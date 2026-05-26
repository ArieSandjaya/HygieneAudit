// ========== DATA STORE ==========
        const Store = {
            tenants: [],        // loaded from API after login
            audits: [],         // loaded from IndexedDB; updated on create/submit
            currentUser: null,  // { id, name, role }
            currentAudit: null, // in-progress audit object
            excelReportData: null,
            viewingAuditId: null,
            previousPage: 'dashboard'
        };

        // Decode JWT payload (used to extract user ID)
        function parseJwt() {
            const token = localStorage.getItem(AppConfig.TOKEN_STORAGE_KEY);
            if (!token) return null;
            try { return JSON.parse(atob(token.split('.')[1])); } catch { return null; }
        }

        // Normalize backend enum strings to frontend convention
        function normalizeStatus(s) {
            if (!s) return null;
            return (s === 'Pass' || s === 'PASS') ? 'PASS' : 'FAIL';
        }
        function toApiStatus(s) {
            if (!s) return null;
            return s === 'PASS' ? 'Pass' : 'Fail';
        }

        // ========== INDEXEDDB OFFLINE STORAGE ==========
        const DB_NAME = 'HygieneAuditDB';
        const DB_VERSION = 1;
        let db = null;

        async function initIndexedDB() {
            return new Promise((resolve, reject) => {
                const request = indexedDB.open(DB_NAME, DB_VERSION);

                request.onerror = () => reject(request.error);
                request.onsuccess = () => {
                    db = request.result;
                    resolve(db);
                };

                request.onupgradeneeded = (event) => {
                    const database = event.target.result;

                    // Store for audits
                    if (!database.objectStoreNames.contains('audits')) {
                        const auditStore = database.createObjectStore('audits', { keyPath: 'id' });
                        auditStore.createIndex('tenantId', 'tenantId', { unique: false });
                        auditStore.createIndex('status', 'status', { unique: false });
                        auditStore.createIndex('date', 'date', { unique: false });
                    }

                    // Store for sync queue
                    if (!database.objectStoreNames.contains('syncQueue')) {
                        const syncStore = database.createObjectStore('syncQueue', { keyPath: 'id', autoIncrement: true });
                        syncStore.createIndex('timestamp', 'timestamp', { unique: false });
                        syncStore.createIndex('synced', 'synced', { unique: false });
                    }

                    // Store for offline photos
                    if (!database.objectStoreNames.contains('photos')) {
                        database.createObjectStore('photos', { keyPath: 'id' });
                    }
                };
            });
        }

        async function saveAuditOffline(audit) {
            if (!db) await initIndexedDB();

            return new Promise((resolve, reject) => {
                const transaction = db.transaction(['audits'], 'readwrite');
                const store = transaction.objectStore('audits');
                const request = store.put(audit);

                request.onsuccess = () => resolve();
                request.onerror = () => reject(request.error);
            });
        }

        async function getAuditsOffline() {
            if (!db) await initIndexedDB();

            return new Promise((resolve, reject) => {
                const transaction = db.transaction(['audits'], 'readonly');
                const store = transaction.objectStore('audits');
                const request = store.getAll();

                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });
        }

        async function addToSyncQueue(action, data) {
            if (!db) await initIndexedDB();

            const syncItem = {
                action: action,
                data: data,
                timestamp: Date.now(),
                synced: false
            };

            return new Promise((resolve, reject) => {
                const transaction = db.transaction(['syncQueue'], 'readwrite');
                const store = transaction.objectStore('syncQueue');
                const request = store.add(syncItem);

                request.onsuccess = () => resolve();
                request.onerror = () => reject(request.error);
            });
        }

        async function syncData() {
            if (!db) await initIndexedDB();
            if (!navigator.onLine) {
                showToast('Tidak ada koneksi internet. Sinkronisasi ditunda.', 'info');
                return;
            }

            showLoading();

            try {
                // Get unsynced items
                const unsynced = await new Promise((resolve, reject) => {
                    const transaction = db.transaction(['syncQueue'], 'readonly');
                    const store = transaction.objectStore('syncQueue');
                    const index = store.index('synced');
                    const request = index.getAll(false);

                    request.onsuccess = () => resolve(request.result);
                    request.onerror = () => reject(request.error);
                });

                if (unsynced.length === 0) {
                    showToast('Semua data sudah tersinkronisasi!');
                    hideLoading();
                    return;
                }

                const markSynced = (item) => new Promise((resolve, reject) => {
                    const tx = db.transaction(['syncQueue'], 'readwrite');
                    const store = tx.objectStore('syncQueue');
                    item.synced = true;
                    const req = store.put(item);
                    req.onsuccess = () => resolve();
                    req.onerror = () => reject(req.error);
                });

                let syncedCount = 0;
                for (const item of unsynced) {
                    try {
                        if (item.action === 'SAVE_DRAFT' && item.data?.id) {
                            await ApiClient.audits.saveDraft(item.data.id);
                        } else {
                            // Generic fallback — forward to /api/sync for server-side processing
                            await ApiClient.sync.push([{
                                action: item.action,
                                entityType: 'audit',
                                entityId: String(item.data?.id || ''),
                                payload: JSON.stringify(item.data),
                                timestamp: item.timestamp || new Date().toISOString(),
                                isSynced: false,
                                retryCount: 0,
                            }]);
                        }
                        await markSynced(item);
                        syncedCount++;
                    } catch { /* leave unsynced, retry next time */ }
                }

                showToast(`${syncedCount} item berhasil disinkronkan!`);
                updateSyncStatus('online');
            } catch (error) {
                showToast('Gagal sinkronisasi: ' + error.message, 'error');
            } finally {
                hideLoading();
            }
        }

        // ========== NETWORK STATUS ==========
        function updateSyncStatus(status) {
            const statusEl = document.getElementById('syncStatus');
            const textEl = document.getElementById('syncText');

            statusEl.className = 'sync-status active ' + status;

            if (status === 'online') {
                textEl.textContent = 'Online';
                setTimeout(() => statusEl.classList.remove('active'), 3000);
            } else if (status === 'offline') {
                textEl.textContent = 'Offline - Menyimpan lokal';
            } else if (status === 'syncing') {
                textEl.textContent = 'Sinkronisasi...';
            }
        }

        window.addEventListener('online', () => {
            updateSyncStatus('online');
            showToast('Koneksi internet tersedia!', 'success');
            syncData(); // Auto sync when back online
        });

        window.addEventListener('offline', () => {
            updateSyncStatus('offline');
            showToast('Koneksi internet terputus. Data disimpan lokal.', 'error');
        });

        // ========== PUSH NOTIFICATION ==========
        async function requestNotificationPermission() {
            if (!('Notification' in window)) {
                showToast('Browser tidak mendukung notifikasi', 'error');
                return;
            }

            // Check if iOS and not standalone
            const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent);
            const isStandalone = window.matchMedia('(display-mode: standalone)').matches ||
                window.navigator.standalone === true;

            if (isIOS && !isStandalone) {
                showToast('Silakan install aplikasi terlebih dahulu (Add to Home Screen)', 'info');
                showInstallPrompt();
                return;
            }

            const permission = await Notification.requestPermission();

            if (permission === 'granted') {
                document.getElementById('notifPermission').classList.remove('active');
                showToast('Notifikasi diaktifkan!');

                // Register service worker for push
                if ('serviceWorker' in navigator) {
                    const registration = await navigator.serviceWorker.ready;

                    // Subscribe to push (requires VAPID keys from server)
                    try {
                        const subscription = await registration.pushManager.subscribe({
                            userVisibleOnly: true,
                            applicationServerKey: urlBase64ToUint8Array('YOUR_VAPID_PUBLIC_KEY')
                        });

                        // Send subscription to server
                        // await fetch('/api/push/subscribe', { method: 'POST', body: JSON.stringify(subscription) });

                        console.log('Push subscription:', subscription);
                    } catch (e) {
                        console.log('Push subscription failed:', e);
                    }
                }
            } else {
                showToast('Izin notifikasi ditolak', 'error');
            }
        }

        function urlBase64ToUint8Array(base64String) {
            const padding = '='.repeat((4 - base64String.length % 4) % 4);
            const base64 = (base64String + padding)
                .replace(/\-/g, '+')
                .replace(/_/g, '/');

            const rawData = window.atob(base64);
            const outputArray = new Uint8Array(rawData.length);

            for (let i = 0; i < rawData.length; ++i) {
                outputArray[i] = rawData.charCodeAt(i);
            }
            return outputArray;
        }

        function showNotification(title, options = {}) {
            if (Notification.permission === 'granted') {
                navigator.serviceWorker.ready.then(registration => {
                    registration.showNotification(title, {
                        body: options.body || '',
                        icon: '/icon-192x192.png',
                        badge: '/badge-72x72.png',
                        tag: options.tag || 'default',
                        requireInteraction: options.requireInteraction || false,
                        actions: options.actions || []
                    });
                });
            }
        }

        // ========== EXPORT FUNCTIONS ==========
        function toggleExportMenu() {
            const menu = document.getElementById('exportMenu');
            menu.classList.toggle('active');
        }

        async function exportToPDF() {
            const audit = Store.currentAudit || Store.audits[Store.audits.length - 1];
            if (!audit) {
                showToast('Tidak ada data audit untuk diexport!', 'error');
                return;
            }

            showLoading();

            try {
                const { jsPDF } = window.jspdf;
                const doc = new jsPDF();

                // Header
                doc.setFontSize(20);
                doc.text('HYGIENE AUDIT REPORT', 105, 20, { align: 'center' });

                doc.setFontSize(12);
                const tenant = Store.tenants.find(t => t.id === audit.tenantId);
                doc.text(`Tenant: ${tenant?.name || 'Unknown'}`, 20, 40);
                doc.text(`Tanggal: ${audit.date}`, 20, 50);
                doc.text(`PIC: ${audit.picName}`, 20, 60);
                doc.text(`Status: ${audit.status}`, 20, 70);

                // Summary
                const totalItems = audit.items?.length || 0;
                const passItems = audit.items?.filter(i => i.status === 'PASS').length || 0;
                const failItems = audit.items?.filter(i => i.status === 'FAIL').length || 0;

                doc.text(`Total Items: ${totalItems}`, 20, 85);
                doc.text(`Pass: ${passItems}`, 20, 95);
                doc.text(`Fail: ${failItems}`, 20, 105);

                // Table
                const tableData = audit.items?.map(item => [
                    item.name,
                    item.status === 'PASS' ? '✓ PASS' : item.status === 'FAIL' ? '✗ FAIL' : '-',
                    item.note || '-'
                ]) || [];

                doc.autoTable({
                    startY: 120,
                    head: [['Item', 'Status', 'Catatan']],
                    body: tableData,
                    theme: 'grid',
                    headStyles: {
                        fillColor: [37, 99, 235],
                        textColor: 255,
                        fontStyle: 'bold'
                    },
                    alternateRowStyles: {
                        fillColor: [249, 250, 251]
                    },
                    columnStyles: {
                        0: { cellWidth: 80 },
                        1: { cellWidth: 30 },
                        2: { cellWidth: 'auto' }
                    }
                });

                // Save
                doc.save(`Audit_${tenant?.name || 'Unknown'}_${audit.date}.pdf`);
                showToast('PDF berhasil diexport!');
            } catch (error) {
                showToast('Gagal export PDF: ' + error.message, 'error');
            } finally {
                hideLoading();
                document.getElementById('exportMenu').classList.remove('active');
            }
        }

        async function exportToExcel() {
            const audit = Store.currentAudit || Store.audits[Store.audits.length - 1];
            if (!audit) {
                showToast('Tidak ada data audit untuk diexport!', 'error');
                return;
            }

            showLoading();

            try {
                const tenant = Store.tenants.find(t => t.id === audit.tenantId);

                // Prepare data
                const auditData = audit.items?.map((item, index) => ({
                    'No': index + 1,
                    'Kategori': item.category,
                    'Item Pemeriksaan': item.name,
                    'Status': item.status || 'Belum Dicek',
                    'Catatan': item.note || '-',
                    'Jumlah Foto': item.photos?.length || 0
                })) || [];

                // Create workbook
                const wb = XLSX.utils.book_new();
                const ws = XLSX.utils.json_to_sheet(auditData);

                // Add header info
                XLSX.utils.sheet_add_aoa(ws, [
                    ['HYGIENE AUDIT REPORT'],
                    ['Tenant:', tenant?.name || 'Unknown'],
                    ['Tanggal:', audit.date],
                    ['PIC:', audit.picName],
                    ['Status:', audit.status],
                    []
                ], { origin: 'A1' });

                // Adjust column widths
                ws['!cols'] = [
                    { wch: 5 },   // No
                    { wch: 20 },  // Kategori
                    { wch: 40 },  // Item
                    { wch: 15 },  // Status
                    { wch: 30 },  // Catatan
                    { wch: 12 }   // Foto
                ];

                XLSX.utils.book_append_sheet(wb, ws, 'Audit Report');

                // Save
                XLSX.writeFile(wb, `Audit_${tenant?.name || 'Unknown'}_${audit.date}.xlsx`);
                showToast('Excel berhasil diexport!');
            } catch (error) {
                showToast('Gagal export Excel: ' + error.message, 'error');
            } finally {
                hideLoading();
                document.getElementById('exportMenu').classList.remove('active');
            }
        }

        // ========== PWA INSTALL PROMPT ==========
        let deferredPrompt = null;

        window.addEventListener('beforeinstallprompt', (e) => {
            e.preventDefault();
            deferredPrompt = e;
        });

        function showInstallPrompt() {
            const isStandalone = window.matchMedia('(display-mode: standalone)').matches ||
                window.navigator.standalone === true;
            const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent);
            const isAndroid = /Android/.test(navigator.userAgent);
            const promptShown = localStorage.getItem('installPromptShown');

            if (isStandalone || promptShown) return;

            const prompt = document.getElementById('installPrompt');
            const steps = document.getElementById('installSteps');

            if (isIOS) {
                steps.innerHTML = `
                        <ol style="padding-left: 20px;">
                            <li>Buka Safari (browser bawaan iPhone)</li>
                            <li>Tap tombol <strong>Share</strong> (kotak dengan panah ke atas)</li>
                            <li>Scroll ke bawah, tap <strong>Add to Home Screen</strong></li>
                            <li>Tap <strong>Add</strong> di pojok kanan atas</li>
                        </ol>
                    `;
            } else if (isAndroid) {
                if (deferredPrompt) {
                    steps.innerHTML = `<p style="text-align: center;">Tap tombol di bawah untuk install</p>`;
                    const btn = document.createElement('button');
                    btn.className = 'btn btn-primary';
                    btn.innerHTML = '📲 Install Sekarang';
                    btn.onclick = () => {
                        deferredPrompt.prompt();
                        deferredPrompt.userChoice.then((choice) => {
                            if (choice.outcome === 'accepted') {
                                localStorage.setItem('installPromptShown', 'true');
                                showToast('Aplikasi berhasil diinstall!');
                            }
                            deferredPrompt = null;
                            dismissInstall();
                        });
                    };
                    steps.appendChild(btn);
                } else {
                    steps.innerHTML = `
                            <ol style="padding-left: 20px;">
                                <li>Tap tombol <strong>Menu</strong> (3 titik) di Chrome</li>
                                <li>Pilih <strong>Add to Home Screen</strong></li>
                                <li>Tap <strong>Install</strong></li>
                            </ol>
                        `;
                }
            } else {
                // Desktop
                steps.innerHTML = `
                        <ol style="padding-left: 20px;">
                            <li>Click icon <strong>Install</strong> di address bar Chrome/Edge</li>
                            <li>Atau click Menu → Install this site as an app</li>
                        </ol>
                    `;
            }

            prompt.classList.add('active');
        }

        function dismissInstall() {
            document.getElementById('installPrompt').classList.remove('active');
            localStorage.setItem('installPromptShown', 'true');
        }

        // ========== UTILITY FUNCTIONS ==========
        function showToast(message, type = 'success') {
            const container = document.getElementById('toastContainer');
            const toast = document.createElement('div');
            toast.className = `toast ${type}`;
            toast.textContent = message;
            container.appendChild(toast);
            setTimeout(() => toast.remove(), 3000);
        }

        function showLoading() {
            document.getElementById('loadingOverlay').classList.add('active');
        }

        function hideLoading() {
            document.getElementById('loadingOverlay').classList.remove('active');
        }

        function formatDate(dateStr) {
            const date = new Date(dateStr);
            const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
            return date.toLocaleDateString('id-ID', options);
        }

        function generateId(prefix) {
            return `${prefix}-${Date.now()}-${Math.random().toString(36).substr(2, 5)}`;
        }

        // ========== OFFLINE SYNC ==========
        const SyncManager = {
            isOnline: navigator.onLine,
            pendingSync: [],

            init() {
                window.addEventListener('online', () => this.handleOnline());
                window.addEventListener('offline', () => this.handleOffline());
                this.loadPendingSync();
                this.updateUI();
                setInterval(() => {
                    if (this.isOnline && this.pendingSync.length > 0) this.sync();
                }, 300000);
            },

            handleOnline() {
                this.isOnline = true;
                this.updateUI();
                showToast('🌐 Koneksi internet tersedia!', 'success');
                if (this.pendingSync.length > 0) setTimeout(() => this.sync(), 2000);
            },

            handleOffline() {
                this.isOnline = false;
                this.updateUI();
                showToast('📴 Mode offline aktif. Data disimpan lokal.', 'info');
            },

            updateUI() {
                const status = document.getElementById('syncStatus');
                const icon = document.getElementById('syncIcon');
                const text = document.getElementById('syncText');
                const action = document.getElementById('syncAction');
                if (!this.isOnline) {
                    status.className = 'sync-status offline active';
                    icon.textContent = '📴';
                    text.textContent = `Offline • ${this.pendingSync.length} pending`;
                    action.style.display = 'none';
                } else if (this.pendingSync.length > 0) {
                    status.className = 'sync-status syncing active';
                    icon.textContent = '🔄';
                    text.textContent = `${this.pendingSync.length} item menunggu sync`;
                    action.style.display = 'block';
                } else {
                    status.className = 'sync-status online active';
                    icon.textContent = '☁️';
                    text.textContent = 'Tersinkronisasi';
                    action.style.display = 'none';
                    setTimeout(() => status.classList.remove('active'), 3000);
                }
            },

            queueForSync(type, data) {
                this.pendingSync.push({
                    id: generateId('SYNC'), type, data,
                    timestamp: new Date().toISOString(), retries: 0
                });
                this.savePendingSync();
                this.updateUI();
                if (this.isOnline) this.sync();
            },

            async sync() {
                if (!this.isOnline || this.pendingSync.length === 0) return;
                document.getElementById('syncIcon').textContent = '⏳';
                showLoading();

                for (let i = this.pendingSync.length - 1; i >= 0; i--) {
                    const item = this.pendingSync[i];
                    try {
                        await ApiClient.sync.push([{
                            action: item.type,
                            entityType: item.data?.entityType || 'audit',
                            entityId: String(item.data?.id || item.id),
                            payload: JSON.stringify(item.data),
                            timestamp: item.timestamp,
                            isSynced: false,
                            retryCount: item.retries
                        }]);
                        this.pendingSync.splice(i, 1);
                    } catch (err) {
                        item.retries = (item.retries || 0) + 1;
                        if (item.retries >= AppConfig.SYNC_RETRY_MAX)
                            showToast(`Gagal sync: ${item.type}`, 'error');
                    }
                }

                this.savePendingSync();
                this.updateUI();
                hideLoading();
                if (this.pendingSync.length === 0) showToast('✅ Semua data tersinkronisasi!');
            },

            savePendingSync() { localStorage.setItem('pendingSync', JSON.stringify(this.pendingSync)); },
            loadPendingSync() {
                const saved = localStorage.getItem('pendingSync');
                if (saved) this.pendingSync = JSON.parse(saved);
            }
        };

        function manualSync() { SyncManager.sync(); }

        // ========== PUSH NOTIFICATION ==========
        const NotificationManager = {
            permission: 'default',

            init() {
                if ('Notification' in window) {
                    this.permission = Notification.permission;
                    if (this.permission === 'default') {
                        const dismissed = localStorage.getItem('notifDismissed');
                        if (!dismissed)
                            setTimeout(() => document.getElementById('notifPermission').classList.add('active'), 5000);
                    }
                }
                this.updateBadge();
            },

            async requestPermission() {
                if (!('Notification' in window)) { showToast('Browser tidak mendukung notifikasi', 'error'); return; }
                const permission = await Notification.requestPermission();
                this.permission = permission;
                if (permission === 'granted') {
                    showToast('🔔 Notifikasi diaktifkan!');
                    new Notification('Hygiene Audit App', { body: 'Notifikasi berhasil diaktifkan!' });
                } else {
                    showToast('Notifikasi ditolak', 'error');
                }
                document.getElementById('notifPermission').classList.remove('active');
            },

            notifyAuditComplete(audit) {
                if (this.permission !== 'granted') return;
                const tenant = Store.tenants.find(t => t.id === audit.tenantId);
                const failCount = audit.items?.filter(i => i.status === 'FAIL').length || 0;
                new Notification('✅ Audit Selesai', {
                    body: `Audit ${tenant?.name} selesai! ${failCount} item FAIL.`
                });
            },

            updateBadge() {
                const completedCount = Store.audits.filter(a => a.status === 'COMPLETED').length;
                const historyNav = document.querySelector('.menu-item[data-page="history"]');
                if (!historyNav) return;
                if (completedCount > 0) {
                    let badge = historyNav.querySelector('.nav-badge');
                    if (!badge) { badge = document.createElement('span'); badge.className = 'menu-badge'; historyNav.querySelector('.menu-link').appendChild(badge); }
                    badge.textContent = completedCount > 99 ? '99+' : completedCount;
                }
            }
        };

        function requestNotification() { NotificationManager.requestPermission(); }
        function dismissNotification() {
            document.getElementById('notifPermission').classList.remove('active');
            localStorage.setItem('notifDismissed', 'true');
        }

        // ========== AUTHENTICATION ==========
        async function _applySession(user) {
            showLoading();

            const isAdmin = ['admin', 'Admin', 'SuperAdmin'].includes(user.role);

            try { Store.tenants = await ApiClient.tenants.getAll(); } catch { }

            // Load audits from API (works in incognito); fall back to IndexedDB if offline
            try {
                const apiAudits = await ApiClient.audits.getAll();
                Store.audits = apiAudits.map(a => {
                    const mappedItems = (a.items || []).map(item => ({
                        ...item,
                        status: item.status || null,
                        note: item.note || '',
                        photos: item.photos || [],
                    }));
                    const cats = {};
                    mappedItems.forEach(item => {
                        if (!cats[item.category]) cats[item.category] = [];
                        cats[item.category].push(item);
                    });
                    return { ...a, items: mappedItems, categories: cats };
                });
            } catch {
                Store.audits = await getAuditsOffline().catch(() => []);
            }

            // Now show app with real data
            document.getElementById('loginPage').style.display = 'none';
            document.getElementById('appContainer').classList.add('active');
            document.getElementById('userName').textContent = user.name;
            document.getElementById('userAvatar').textContent = user.name.charAt(0).toUpperCase();
            document.getElementById('dashboardUserName').textContent = user.name;
            document.getElementById('dashboardDate').textContent = formatDate(new Date());
            document.querySelectorAll('.admin-nav').forEach(el => {
                el.style.display = isAdmin ? 'block' : 'none';
            });
            document.getElementById('sidebarAvatar').textContent = user.name.charAt(0).toUpperCase();
            document.getElementById('sidebarUserName').textContent = user.name;
            document.getElementById('sidebarUserRole').textContent = user.role;

            hideLoading();
            initDashboard();
            showToast(`Selamat datang, ${user.name}!`);
            setTimeout(showInstallPrompt, 2000);
            if ('Notification' in window && Notification.permission === 'default') {
                setTimeout(() => document.getElementById('notifPermission').classList.add('active'), 3000);
            }
            if (!navigator.onLine) updateSyncStatus('offline');
        }

        async function handleLogin(e) {
            e.preventDefault();
            showLoading();
            try {
                const username = document.getElementById('loginUsername').value.trim();
                const password = document.getElementById('loginPassword').value;

                await initIndexedDB();

                const authResponse = await ApiClient.auth.login(username, password);
                ApiClient.saveSession(authResponse);

                const saved = ApiClient.getUser();
                Store.currentUser = { id: saved.id, name: saved.name, role: saved.role };

                await _applySession(Store.currentUser);
            } catch (err) {
                showToast(err.message || 'Username atau password salah!', 'error');
            } finally {
                hideLoading();
            }
        }

        function logout() {
            ApiClient.clearSession();
            Store.currentUser = null;
            Store.currentAudit = null;
            Store.tenants = [];
            Store.audits = [];
            document.getElementById('loginPage').style.display = 'flex';
            document.getElementById('appContainer').classList.remove('active');
            document.getElementById('loginUsername').value = '';
            document.getElementById('loginPassword').value = '';
            document.querySelectorAll('.admin-nav').forEach(el => { el.style.display = 'none'; });
            closeMenu();
            document.getElementById('notifPermission').classList.remove('active');
            showToast('Berhasil logout');
        }

        // ========== NAVIGATION ==========
        function goBack() {
            navigateTo(Store.previousPage || 'dashboard');
        }

        function navigateTo(page) {
            closeMenu();
            const pagesWithBack = ['auditDetail', 'checklist'];
            const currentPage = document.querySelector('.page.active')?.id;
            if (currentPage) {
                const reverseMap = {
                    'pageDashboard': 'dashboard', 'pageNewAudit': 'newAudit',
                    'pageHistory': 'history',
                    'pageAdminUsers': 'adminUsers', 'pageAdminTemplates': 'adminTemplates',
                    'pageAdminTenants': 'adminTenants', 'pageAdminReports': 'adminReports',
                    'pageChecklist': 'checklist', 'pageAuditDetail': 'auditDetail'
                };
                Store.previousPage = reverseMap[currentPage] || 'dashboard';
            }

            // Hide all pages
            document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
            document.querySelectorAll('.menu-item[data-page]').forEach(n => n.classList.remove('active'));

            const backBtn = document.getElementById('backBtn');
            if (backBtn) backBtn.style.display = pagesWithBack.includes(page) ? 'inline-flex' : 'none';

            // Show target page
            const pageMap = {
                'dashboard': 'pageDashboard',
                'newAudit': 'pageNewAudit',
                'history': 'pageHistory',
                'adminUsers': 'pageAdminUsers',
                'adminTemplates': 'pageAdminTemplates',
                'adminTenants': 'pageAdminTenants',
                'adminReports': 'pageAdminReports',
                'checklist': 'pageChecklist',
                'auditDetail': 'pageAuditDetail'
            };

            document.getElementById(pageMap[page]).classList.add('active');

            // Update nav
            const navMap = {
                'dashboard': 0,
                'newAudit': 1,
                'history': 2,
                'adminUsers': 3, 'adminTemplates': 4, 'adminTenants': 5, 'adminReports': 6
            };

            const activeMenuItem = document.querySelector(`.menu-item[data-page="${page}"]`);
            if (activeMenuItem) activeMenuItem.classList.add('active');

            // Update header title
            const titles = {
                'dashboard': 'Dashboard',
                'newAudit': 'Audit Baru',
                'history': 'History Audit',
                'adminUsers': 'Manajemen User',
                'adminTemplates': 'Template Checklist',
                'adminTenants': 'Master Tenant',
                'adminReports': 'Reporting Audit',
                'checklist': 'Detail Audit',
                'auditDetail': 'Detail Hasil Audit'
            };
            document.getElementById('pageTitle').textContent = titles[page] || 'Hygiene Audit';

            // Page-specific init
            if (page === 'dashboard') initDashboard();
            if (page === 'newAudit') initNewAudit();
            if (page === 'history') initHistory();
            if (page === 'adminUsers') initAdminUsers();
            if (page === 'adminTemplates') initAdminTemplates();
            if (page === 'adminTenants') initAdminTenants();
            if (page === 'adminReports') initAdminReports();
        }

        // ========== DASHBOARD ==========
        function initDashboard() {
            const audits = Store.audits;
            document.getElementById('statTotal').textContent = audits.length;
            document.getElementById('statCompleted').textContent = audits.filter(a => a.status === 'COMPLETED').length;
            document.getElementById('statDraft').textContent = audits.filter(a => a.status === 'DRAFT').length;

            const failItems = audits.reduce((acc, audit) => {
                return acc + (audit.items?.filter(i => i.status === 'FAIL').length || 0);
            }, 0);
            document.getElementById('statFail').textContent = failItems;

            // Recent audits
            const recentList = document.getElementById('recentAudits');
            const recent = [...audits].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt)).slice(0, 5);

            if (recent.length === 0) {
                recentList.innerHTML = `
                        <div class="empty-state">
                            <div class="icon">📋</div>
                            <h3>Belum ada audit</h3>
                            <p>Mulai audit pertama Anda sekarang</p>
                        </div>
                    `;
            } else {
                recentList.innerHTML = recent.map(audit => {
                    const totalItems = audit.items?.length || 0;
                    const passItems = audit.items?.filter(i => i.status === 'PASS').length || 0;
                    const passRate = totalItems > 0 ? Math.round((passItems / totalItems) * 100) : 0;
                    const tenant = Store.tenants.find(t => t.id === audit.tenantId);

                    return `
                            <div class="audit-card" onclick="${audit.status === 'DRAFT' ? `resumeAudit('${audit.id}')` : `viewAuditDetail('${audit.id}')`}">
                                <div class="audit-card-header">
                                    <div>
                                        <div class="audit-card-title">${tenant?.name || 'Unknown Tenant'}</div>
                                        <div class="audit-card-subtitle">${formatDate(audit.date)} • PIC: ${audit.picName}</div>
                                    </div>
                                    <span class="status-badge ${audit.status.toLowerCase()}">${audit.status === 'DRAFT' ? '✏️ DRAFT' : audit.status}</span>
                                </div>
                                <div class="audit-card-footer">
                                    <span class="audit-meta">${audit.status === 'DRAFT' ? 'Ketuk untuk melanjutkan' : `${totalItems} item diperiksa`}</span>
                                    <div class="pass-rate">
                                        <div class="rate-bar"><div class="fill" style="width: ${passRate}%"></div></div>
                                        <span>${passRate}% Pass</span>
                                    </div>
                                </div>
                            </div>
                        `;
                }).join('');
            }
        }

        // ========== NEW AUDIT ==========

        // ========== SEARCHABLE TENANT DROPDOWN ==========
        let _tenantSelectedId = null;

        function renderTenantOptions(query) {
            const list = document.getElementById('tenantOptionsList');
            if (!list) return;
            const q = (query || '').toLowerCase().trim();
            const filtered = q
                ? Store.tenants.filter(t => t.name.toLowerCase().includes(q))
                : Store.tenants;
            if (filtered.length === 0) {
                list.innerHTML = '<li class="ss-no-result">Tenant tidak ditemukan</li>';
            } else {
                list.innerHTML = filtered.map(t =>
                    `<li class="${_tenantSelectedId === t.id ? 'selected' : ''}"
                         onclick="selectTenantOption(${t.id}, event)">${t.name}</li>`
                ).join('');
            }
        }

        function toggleTenantDropdown(e) {
            if (e) e.stopPropagation();
            const dd = document.getElementById('tenantDropdown');
            if (!dd) return;
            const isOpen = dd.classList.contains('open');
            if (isOpen) {
                closeTenantDropdown();
            } else {
                dd.classList.add('open');
                const input = document.getElementById('tenantSearchInput');
                if (input) { input.value = ''; input.focus(); }
                renderTenantOptions('');
            }
        }

        function closeTenantDropdown() {
            const dd = document.getElementById('tenantDropdown');
            if (dd) dd.classList.remove('open');
        }

        function filterTenantOptions(query) {
            renderTenantOptions(query);
        }

        function selectTenantOption(id, e) {
            if (e) e.stopPropagation();
            const tenant = Store.tenants.find(t => t.id === id);
            if (!tenant) return;
            _tenantSelectedId = id;
            document.getElementById('auditTenant').value = id;
            const triggerEl = document.getElementById('tenantSelectedText');
            triggerEl.textContent = tenant.name;
            triggerEl.closest('.searchable-select-trigger').classList.add('has-value');
            closeTenantDropdown();
            onTenantSelect();
        }

        // ========== TENANT HISTORY PREVIEW ==========
        async function onTenantSelect() {
            const tenantId = parseInt(document.getElementById('auditTenant').value);
            const preview = document.getElementById('tenantHistoryPreview');
            const badge = document.getElementById('tenantHistoryBadge');

            if (!tenantId) {
                preview.classList.remove('active');
                badge.classList.remove('active');
                return;
            }

            const tenant = Store.tenants.find(t => t.id === tenantId);
            if (!tenant) return;

            document.getElementById('previewTenantType').textContent = tenant.usesGas ? '🔥 Gas' : '⚡ Non-Gas';
            document.getElementById('previewTenantType').className = `tenant-type ${tenant.usesGas ? 'gas' : 'non-gas'}`;

            // Auto-set gas toggle from tenant default
            document.getElementById('auditGas').checked = tenant.usesGas;
            updateGasLabel();

            try {
                const history = await ApiClient.tenants.getHistory(tenantId);

                if (history.totalAudits === 0) {
                    preview.querySelector('.preview-subtitle').textContent = 'Belum pernah diaudit';
                    preview.querySelector('.history-stats').style.display = 'none';
                    preview.querySelector('.trend-chart').style.display = 'none';
                    preview.querySelector('.recent-audits-list').innerHTML = `
                            <div class="no-history"><div class="icon">📭</div><p>Tenant ini belum memiliki riwayat audit</p></div>`;
                    badge.classList.remove('active');
                    preview.classList.add('active');
                    return;
                }

                badge.textContent = `${history.totalAudits} audit`;
                badge.classList.add('active');

                preview.querySelector('.history-stats').style.display = 'grid';
                preview.querySelector('.trend-chart').style.display = 'block';
                preview.querySelector('.preview-subtitle').textContent = `${history.totalAudits} audit ditemukan`;

                document.getElementById('previewTotalAudits').textContent = history.totalAudits;
                document.getElementById('previewAvgPass').textContent = `${history.averagePassRate}%`;
                document.getElementById('previewLastAudit').textContent = history.daysSinceLastAudit;

                // recentAudits is newest-first; reverse for chronological chart
                renderTrendChart([...history.recentAudits].reverse());
                renderRecentAuditsList(history.recentAudits.slice(0, 5));
                preview.classList.add('active');
            } catch (err) {
                showToast('Gagal memuat riwayat tenant: ' + err.message, 'error');
            }
        }

        // Accepts RecentAuditDto from API: { id, date, picName, totalItems, passItems, failItems, passRate }
        function renderTrendChart(audits) {
            const container = document.getElementById('previewTrendBars');
            if (!audits.length) {
                container.innerHTML = '<div style="text-align: center; color: var(--gray-400); font-size: 12px;">Tidak cukup data</div>';
                return;
            }
            container.innerHTML = audits.map(audit => {
                const rate = audit.passRate ?? 0;
                const height = Math.max((rate / 100) * 60, 4);
                const date = new Date(audit.date);
                return `
                        <div class="trend-bar-wrapper">
                            <div class="trend-bar ${rate >= 70 ? 'pass' : 'fail'}" style="height: ${height}px;">
                                <span class="trend-bar-value">${rate}%</span>
                            </div>
                            <span class="trend-bar-label">${date.getDate()} ${date.toLocaleDateString('id-ID', { month: 'short' })}</span>
                        </div>
                    `;
            }).join('');
        }

        function renderRecentAuditsList(audits) {
            const container = document.getElementById('previewRecentAudits');
            container.innerHTML = audits.map(audit => {
                const rate = audit.passRate ?? 0;
                const total = audit.totalItems ?? 0;
                const dotColor = rate >= 90 ? 'var(--success)' : rate >= 70 ? 'var(--warning)' : 'var(--danger)';
                return `
                        <div class="recent-audit-item">
                            <div class="recent-audit-dot" style="background: ${dotColor};"></div>
                            <div class="recent-audit-info">
                                <div class="recent-audit-date">${formatDate(audit.date)}</div>
                                <div class="recent-audit-pic">PIC: ${audit.picName} • ${total} item</div>
                            </div>
                            <div class="recent-audit-rate ${rate >= 70 ? 'good' : 'bad'}">${rate}%</div>
                        </div>
                    `;
            }).join('');
        }

        function initNewAudit() {
            document.getElementById('auditDate').valueAsDate = new Date();

            // Reset searchable tenant dropdown
            document.getElementById('auditTenant').value = '';
            const triggerEl = document.getElementById('tenantSelectedText');
            triggerEl.textContent = 'Pilih Tenant';
            triggerEl.closest('.searchable-select-trigger').classList.remove('has-value');
            _tenantSelectedId = null;
            closeTenantDropdown();
            renderTenantOptions('');

            // Auto-set PIC to current logged-in user (no users-list API endpoint)
            const picSelect = document.getElementById('auditPIC');
            if (Store.currentUser) {
                picSelect.innerHTML = `<option value="${Store.currentUser.id}" selected>${Store.currentUser.name}</option>`;
                picSelect.disabled = true;
            }

            document.getElementById('tenantHistoryPreview').classList.remove('active');
            document.getElementById('tenantHistoryBadge').classList.remove('active');
        }

        function updateGasLabel() {
            const isGas = document.getElementById('auditGas').checked;
            document.getElementById('gasLabel').textContent = isGas ? 'Gas' : 'Non-Gas';
        }

        async function startAudit() {
            const date = document.getElementById('auditDate').value;
            const tenantId = parseInt(document.getElementById('auditTenant').value);
            const picId = Store.currentUser?.id;
            const isGas = document.getElementById('auditGas').checked;

            if (!date || !tenantId) {
                showToast('Mohon lengkapi semua field!', 'error');
                return;
            }
            if (!picId) {
                showToast('Sesi tidak valid, silakan login ulang.', 'error');
                return;
            }

            showLoading();
            try {
                const audit = await ApiClient.audits.create({ date, tenantId, picId, isGas });

                // Normalize items from API (status enum: null / "Pass" / "Fail")
                const items = (audit.items || []).map(item => ({
                    ...item,
                    status: normalizeStatus(item.status),
                    note: item.note || '',
                    photos: (item.photos || []).map(p => p.photoUrl || p)
                }));

                // Build category map for checklist renderer
                const categories = {};
                items.forEach(item => {
                    if (!categories[item.category]) categories[item.category] = [];
                    categories[item.category].push(item);
                });

                const tenant = Store.tenants.find(t => t.id === tenantId);
                Store.currentAudit = {
                    ...audit,
                    items,
                    categories,
                    tenantName: audit.tenant?.name || tenant?.name || '',
                    picName: audit.pic?.name || Store.currentUser.name || '',
                    status: 'DRAFT'
                };

                await saveAuditOffline(Store.currentAudit);
                renderChecklist();
                navigateTo('checklist');
                showToast('Audit dimulai! Silakan isi checklist.');
            } catch (err) {
                showToast('Gagal membuat audit: ' + err.message, 'error');
            } finally {
                hideLoading();
            }
        }

        async function resumeAudit(auditId) {
            showLoading();
            try {
                const audit = await ApiClient.audits.getById(auditId);
                const items = (audit.items || []).map(item => ({
                    ...item,
                    status: normalizeStatus(item.status),
                    note: item.note || '',
                    photos: (item.photos || []).map(p => p.photoUrl || p)
                }));
                const categories = {};
                items.forEach(item => {
                    if (!categories[item.category]) categories[item.category] = [];
                    categories[item.category].push(item);
                });
                Store.currentAudit = { ...audit, items, categories, status: 'DRAFT' };
                await saveAuditOffline(Store.currentAudit);
                renderChecklist();
                navigateTo('checklist');
            } catch (err) {
                showToast('Gagal memuat audit: ' + err.message, 'error');
            } finally {
                hideLoading();
            }
        }


        function renderChecklist() {
            const audit = Store.currentAudit;
            if (!audit) return;

            const container = document.getElementById('checklistContainer');
            const categories = audit.categories;

            let html = '';
            let totalItems = 0;
            let checkedItems = 0;

            for (const [category, items] of Object.entries(categories)) {
                const categoryIcon = getCategoryIcon(category);
                const checkedCount = items.filter(item => item.status !== null).length;

                totalItems += items.length;
                checkedItems += checkedCount;

                html += `
                        <div class="category-section">
                            <div class="category-header" onclick="toggleCategory(this)">
                                <div class="category-title">
                                    <span class="icon">${categoryIcon}</span>
                                    <span>${category}</span>
                                </div>
                                <span class="category-progress">${checkedCount}/${items.length}</span>
                            </div>
                            <div class="category-items">
                                ${items.map(item => {
                    const isPass = item.status === 'PASS';
                    const isFail = item.status === 'FAIL';
                    const noteRequired = isFail;

                    return `
                                        <div class="checklist-item" data-item-id="${item.templateId}">
                                            <div class="checklist-item-header">
                                                <div class="checklist-item-name">${item.name}</div>
                                                <div class="status-toggle">
                                                    <button class="status-btn ${isPass ? 'pass' : ''}"
                                                            onclick="setItemStatus(${item.templateId}, 'PASS')"
                                                            title="Pass">😊</button>
                                                    <button class="status-btn ${isFail ? 'fail' : ''}"
                                                            onclick="setItemStatus(${item.templateId}, 'FAIL')"
                                                            title="Fail">😡</button>
                                                </div>
                                            </div>

                                            <div class="checklist-item-note">
                                                <div class="note-label">
                                                    Catatan ${noteRequired ? '<span class="required-mark">*</span>' : ''}
                                                </div>
                                                <textarea class="note-input ${noteRequired ? 'required' : ''}"
                                                          placeholder="Tambahkan catatan..."
                                                          onchange="updateNote(${item.templateId}, this.value)"
                                                          rows="2">${item.note}</textarea>
                                            </div>

                                            <div class="photo-section">
                                                <div class="photo-grid" id="photoGrid-${item.templateId}">
                                                    ${item.photos.map((photo, idx) => `
                                                        <div class="photo-thumb" onclick="viewPhoto('${photo}')">
                                                            <img src="${photo}" alt="Evidence">
                                                            <button class="remove-btn" onclick="event.stopPropagation(); removePhoto(${item.templateId}, ${idx})">&times;</button>
                                                        </div>
                                                    `).join('')}
                                                </div>
                                                <div class="photo-actions">
                                                    <button class="photo-btn" onclick="triggerCamera(${item.templateId})">
                                                        <span class="icon">📷</span>
                                                        <span>Kamera</span>
                                                    </button>
                                                    <button class="photo-btn" onclick="triggerGallery(${item.templateId})">
                                                        <span class="icon">🖼️</span>
                                                        <span>Galeri</span>
                                                    </button>
                                                </div>
                                                <input type="file" class="hidden-input" id="camera-${item.templateId}"
                                                       accept="image/*" capture="environment" onchange="handlePhoto(${item.templateId}, this)">
                                                <input type="file" class="hidden-input" id="gallery-${item.templateId}"
                                                       accept="image/*" multiple onchange="handlePhoto(${item.templateId}, this)">
                                            </div>
                                        </div>
                                    `;
                }).join('')}
                            </div>
                        </div>
                    `;
            }

            container.innerHTML = html;

            // Update progress
            document.getElementById('progressText').textContent = `${checkedItems}/${totalItems}`;
            document.getElementById('progressFill').style.width = totalItems > 0 ? `${(checkedItems / totalItems) * 100}%` : '0%';
        }

        function getCategoryIcon(category) {
            const icons = {
                'LIFE SAFETY': '🔥',
                'SIRKULASI UDARA': '💨',
                'INSTALASI PIPA AIR BERSIH': '💧',
                'SALURAN PEMBUANGAN': '🚰',
                'INSTALASI LISTRIK': '⚡',
                'KEBERSIHAN AREA': '🧹',
                'PEST CONTROL': '🐜',
                'PERSONAL HYGIENE': '🧼',
                'SERTIFIKASI': '📜'
            };
            return icons[category] || '📋';
        }

        function toggleCategory(header) {
            header.classList.toggle('collapsed');
            const items = header.nextElementSibling;
            items.style.display = items.style.display === 'none' ? 'block' : 'none';
        }

        async function setItemStatus(templateId, status) {
            const item = Store.currentAudit?.items.find(i => i.templateId === templateId);
            if (!item) return;

            item.status = status;
            renderChecklist();

            // Sync to backend (best-effort; queue for offline retry on failure)
            try {
                await ApiClient.audits.updateItem(Store.currentAudit.id, templateId, {
                    status: toApiStatus(status),
                    note: item.note || null,
                    photos: item.photos || []
                });
            } catch (err) {
                SyncManager.queueForSync('update_item', {
                    auditId: Store.currentAudit.id, templateId, status, note: item.note
                });
            }
        }

        const _noteTimers = {};
        function updateNote(templateId, value) {
            const item = Store.currentAudit?.items.find(i => i.templateId === templateId);
            if (!item) return;
            item.note = value;

            // Debounce API call by 800ms
            clearTimeout(_noteTimers[templateId]);
            _noteTimers[templateId] = setTimeout(async () => {
                try {
                    await ApiClient.audits.updateItem(Store.currentAudit.id, templateId, {
                        status: toApiStatus(item.status),
                        note: value || null,
                        photos: item.photos || []
                    });
                } catch { }
            }, 800);
        }

        // ========== PHOTO HANDLING ==========
        function triggerCamera(templateId) {
            document.getElementById(`camera-${templateId}`).click();
        }

        function triggerGallery(templateId) {
            document.getElementById(`gallery-${templateId}`).click();
        }

        function handlePhoto(templateId, input) {
            const files = input.files;
            if (!files.length) return;

            const item = Store.currentAudit.items.find(i => i.templateId === templateId);
            if (!item) return;

            const readFile = f => new Promise((res, rej) => {
                const reader = new FileReader();
                reader.onload = e => res(e.target.result);
                reader.onerror = rej;
                reader.readAsDataURL(f);
            });

            Promise.all(Array.from(files).map(readFile)).then(async dataUrls => {
                dataUrls.forEach(url => item.photos.push(url));
                renderChecklist();
                showToast('Foto berhasil ditambahkan');
                try {
                    await ApiClient.audits.updateItem(Store.currentAudit.id, templateId, {
                        status: toApiStatus(item.status),
                        note: item.note || null,
                        photos: item.photos,
                    });
                } catch {
                    SyncManager.queueForSync('update_item', {
                        auditId: Store.currentAudit.id, templateId,
                        status: item.status, note: item.note, photos: item.photos,
                    });
                }
            });

            input.value = '';
        }

        async function removePhoto(templateId, index) {
            const item = Store.currentAudit.items.find(i => i.templateId === templateId);
            if (!item) return;
            item.photos.splice(index, 1);
            renderChecklist();
            showToast('Foto dihapus');
            try {
                await ApiClient.audits.updateItem(Store.currentAudit.id, templateId, {
                    status: toApiStatus(item.status),
                    note: item.note || null,
                    photos: item.photos,
                });
            } catch {
                SyncManager.queueForSync('update_item', {
                    auditId: Store.currentAudit.id, templateId,
                    status: item.status, note: item.note, photos: item.photos,
                });
            }
        }

        function viewPhoto(src) {
            document.getElementById('viewerImage').src = src;
            document.getElementById('photoViewer').classList.add('active');
        }

        function closePhotoViewer() {
            document.getElementById('photoViewer').classList.remove('active');
        }

        // ========== SAVE & SUBMIT ==========
        async function saveDraft() {
            if (!Store.currentAudit) return;
            showLoading();
            try {
                if (navigator.onLine) {
                    await ApiClient.audits.saveDraft(Store.currentAudit.id);
                } else {
                    await addToSyncQueue('SAVE_DRAFT', { id: Store.currentAudit.id });
                }

                Store.currentAudit.status = 'DRAFT';
                Store.currentAudit.updatedAt = new Date().toISOString();
                await saveAuditOffline(Store.currentAudit);

                const idx = Store.audits.findIndex(a => a.id === Store.currentAudit.id);
                if (idx >= 0) Store.audits[idx] = Store.currentAudit;
                else Store.audits.push(Store.currentAudit);

                showToast('Draft berhasil disimpan!');
                navigateTo('dashboard');
            } catch (err) {
                showToast('Gagal menyimpan draft: ' + err.message, 'error');
            } finally {
                hideLoading();
            }
        }

        async function submitAudit() {
            if (!Store.currentAudit) return;

            // Client-side validation (backend also validates)
            const unchecked = Store.currentAudit.items.filter(i => i.status === null);
            if (unchecked.length > 0) {
                showToast(`Masih ada ${unchecked.length} item yang belum dicek!`, 'error');
                return;
            }
            const failWithoutNote = Store.currentAudit.items.filter(i => i.status === 'FAIL' && !i.note?.trim());
            if (failWithoutNote.length > 0) {
                showToast('Catatan wajib diisi untuk item dengan status FAIL!', 'error');
                return;
            }

            showLoading();
            try {
                await ApiClient.audits.submit(Store.currentAudit.id);

                Store.currentAudit.status = 'COMPLETED';
                Store.currentAudit.completedAt = new Date().toISOString();
                await saveAuditOffline(Store.currentAudit);

                const completedAudit = { ...Store.currentAudit };
                const idx = Store.audits.findIndex(a => a.id === completedAudit.id);
                if (idx >= 0) Store.audits[idx] = completedAudit;
                else Store.audits.push(completedAudit);

                Store.currentAudit = null;
                NotificationManager.notifyAuditComplete(completedAudit);
                NotificationManager.updateBadge();

                showToast('Audit berhasil diselesaikan! 🎉');
                navigateTo('dashboard');
            } catch (err) {
                showToast(err.message || 'Gagal mengirim audit!', 'error');
            } finally {
                hideLoading();
            }
        }

        // ========== HISTORY ==========
        let historyFilter = 'all';
        let historySearch = '';

        function initHistory() {
            // Reset search on page enter
            historySearch = '';
            const input = document.getElementById('historySearchInput');
            if (input) input.value = '';
            const clearBtn = document.getElementById('historyClearBtn');
            if (clearBtn) clearBtn.style.display = 'none';
            renderHistory();
        }

        function filterHistory(filter) {
            historyFilter = filter;
            document.querySelectorAll('.filter-chip').forEach(c => c.classList.remove('active'));
            event.target.classList.add('active');
            renderHistory();
        }

        function searchHistory(query) {
            historySearch = query.trim().toLowerCase();
            const clearBtn = document.getElementById('historyClearBtn');
            if (clearBtn) clearBtn.style.display = historySearch ? 'flex' : 'none';
            renderHistory();
        }

        function clearHistorySearch() {
            historySearch = '';
            const input = document.getElementById('historySearchInput');
            if (input) { input.value = ''; input.focus(); }
            const clearBtn = document.getElementById('historyClearBtn');
            if (clearBtn) clearBtn.style.display = 'none';
            renderHistory();
        }

        function renderHistory() {
            const container = document.getElementById('historyContainer');
            let audits = Store.audits;

            if (historyFilter !== 'all') {
                audits = audits.filter(a => a.status.toLowerCase() === historyFilter);
            }

            if (historySearch) {
                audits = audits.filter(a => {
                    const tenant = Store.tenants.find(t => t.id === a.tenantId);
                    const tenantName = (tenant?.name || '').toLowerCase();
                    const picName = (a.picName || '').toLowerCase();
                    return tenantName.includes(historySearch) || picName.includes(historySearch);
                });
            }

            // Group by tenant
            const tenantGroups = {};
            audits.forEach(audit => {
                if (!tenantGroups[audit.tenantId]) {
                    tenantGroups[audit.tenantId] = {
                        tenant: Store.tenants.find(t => t.id === audit.tenantId),
                        audits: []
                    };
                }
                tenantGroups[audit.tenantId].audits.push(audit);
            });

            if (Object.keys(tenantGroups).length === 0) {
                container.innerHTML = historySearch
                    ? `<div class="empty-state">
                            <div class="icon">🔍</div>
                            <h3>Tidak ditemukan</h3>
                            <p>Tidak ada audit yang cocok dengan "<strong>${historySearch}</strong>"</p>
                        </div>`
                    : `<div class="empty-state">
                            <div class="icon">📜</div>
                            <h3>Belum ada history</h3>
                            <p>Audit yang telah dilakukan akan muncul di sini</p>
                        </div>`;
                return;
            }

            container.innerHTML = Object.values(tenantGroups).map(group => {
                const sortedAudits = group.audits.sort((a, b) => new Date(b.date) - new Date(a.date));

                return `
                        <div class="tenant-history-card">
                            <div class="tenant-history-header">
                                <span class="tenant-name">${group.tenant?.name || 'Unknown'}</span>
                                <span class="tenant-type ${group.tenant?.usesGas ? 'gas' : 'non-gas'}">
                                    ${group.tenant?.usesGas ? '🔥 Gas' : '⚡ Non-Gas'}
                                </span>
                            </div>
                            <div class="history-timeline">
                                ${sortedAudits.map(audit => {
                    const totalItems = audit.items?.length || 0;
                    const failItems = audit.items?.filter(i => i.status === 'FAIL').length || 0;
                    const passRate = totalItems > 0 ? Math.round(((totalItems - failItems) / totalItems) * 100) : 0;
                    const dotClass = passRate >= 90 ? 'pass' : passRate >= 70 ? 'mixed' : 'fail';

                    return `
                                        <div class="timeline-item" onclick="${audit.status === 'DRAFT' ? `resumeAudit('${audit.id}')` : `viewAuditDetail('${audit.id}')`}">
                                            <div class="timeline-dot ${audit.status === 'DRAFT' ? 'mixed' : dotClass}"></div>
                                            <div class="timeline-content">
                                                <div class="timeline-date">${formatDate(audit.date)} ${audit.status === 'DRAFT' ? '<span style="font-size:11px;color:var(--warning);">✏️ Draft</span>' : ''}</div>
                                                <div class="timeline-pic">PIC: ${audit.picName}</div>
                                                <div class="timeline-result">
                                                    ${audit.status === 'DRAFT'
                            ? `<span class="result-badge" style="background:var(--warning);color:#fff;">Lanjutkan</span>`
                            : `<span class="result-badge ${passRate >= 90 ? 'good' : 'bad'}">${passRate}% Pass</span>`
                        }
                                                    <span style="font-size: 12px; color: var(--gray-400);">
                                                        ${failItems} fail / ${totalItems} item
                                                    </span>
                                                </div>
                                            </div>
                                        </div>
                                    `;
                }).join('')}
                            </div>
                        </div>
                    `;
            }).join('');
        }

        // ========== AUDIT DETAIL ==========
        function viewAuditDetail(auditId) {
            const audit = Store.audits.find(a => a.id === auditId);
            if (!audit) return;
            Store.viewingAuditId = auditId;

            const tenant = Store.tenants.find(t => t.id === audit.tenantId);
            const totalItems = audit.items?.length || 0;
            const passItems = audit.items?.filter(i => i.status === 'PASS').length || 0;
            const failItems = audit.items?.filter(i => i.status === 'FAIL').length || 0;
            const passRate = totalItems > 0 ? Math.round((passItems / totalItems) * 100) : 0;

            // Group items by category
            const categories = {};
            audit.items?.forEach(item => {
                if (!categories[item.category]) categories[item.category] = [];
                categories[item.category].push(item);
            });

            let html = `
                    <div class="form-section">
                        <div class="audit-card-header" style="margin-bottom: 20px;">
                            <div>
                                <div class="audit-card-title">${tenant?.name || 'Unknown'}</div>
                                <div class="audit-card-subtitle">${formatDate(audit.date)} • PIC: ${audit.picName}</div>
                            </div>
                            <span class="status-badge ${audit.status.toLowerCase()}">${audit.status}</span>
                        </div>

                        <div class="stats-grid" style="margin-bottom: 20px;">
                            <div class="stat-card success">
                                <div class="stat-icon">✅</div>
                                <div class="stat-info">
                                    <div class="stat-value">${passItems}</div>
                                    <div class="stat-label">Pass</div>
                                </div>
                            </div>
                            <div class="stat-card danger">
                                <div class="stat-icon">❌</div>
                                <div class="stat-info">
                                    <div class="stat-value">${failItems}</div>
                                    <div class="stat-label">Fail</div>
                                </div>
                            </div>
                            <div class="stat-card primary">
                                <div class="stat-icon">📊</div>
                                <div class="stat-info">
                                    <div class="stat-value">${passRate}%</div>
                                    <div class="stat-label">Pass Rate</div>
                                </div>
                            </div>
                        </div>

                        <div style="display: flex; gap: 10px; margin-bottom: 20px; flex-wrap: wrap;">
                            ${audit.status === 'DRAFT' ? `<button class="btn btn-sm btn-warning" onclick="resumeAudit('${auditId}')">✏️ Lanjutkan Audit</button>` : ''}
                            <button class="btn btn-sm btn-primary" onclick="exportCurrentAuditToPDF()">📄 Export PDF</button>
                            <button class="btn btn-sm btn-success" onclick="exportCurrentAuditToExcel()">📊 Export Excel</button>
                        </div>
                    </div>
                `;

            for (const [category, items] of Object.entries(categories)) {
                const categoryIcon = getCategoryIcon(category);
                html += `
                        <div class="form-section">
                            <div class="form-section-title">${categoryIcon} ${category}</div>
                            ${items.map(item => `
                                <div class="checklist-item" style="padding: 16px 0; border-bottom: 1px solid var(--gray-100);">
                                    <div class="checklist-item-header">
                                        <div class="checklist-item-name">${item.name}</div>
                                        <span style="font-size: 24px;">${item.status === 'PASS' ? '😊' : '😡'}</span>
                                    </div>
                                    ${item.note ? `<p style="margin-top: 8px; font-size: 13px; color: var(--gray-600); background: var(--gray-50); padding: 10px; border-radius: 8px;">📝 ${item.note}</p>` : ''}
                                    ${item.photos.length > 0 ? `
                                        <div class="photo-grid" style="margin-top: 10px; grid-template-columns: repeat(4, 1fr);">
                                            ${item.photos.map(photo => `
                                                <div class="photo-thumb" onclick="viewPhoto('${photo}')">
                                                    <img src="${photo}" alt="Evidence">
                                                </div>
                                            `).join('')}
                                        </div>
                                    ` : ''}
                                </div>
                            `).join('')}
                        </div>
                    `;
            }

            document.getElementById('auditDetailContent').innerHTML = html;
            navigateTo('auditDetail');
        }

        // Export from detail view
        async function exportCurrentAuditToPDF() {
            const auditId = Store.viewingAuditId;
            if (auditId) {
                Store.currentAudit = Store.audits.find(a => a.id === auditId);
                await exportToPDF();
            }
        }

        async function exportCurrentAuditToExcel() {
            const auditId = Store.viewingAuditId;
            if (auditId) {
                Store.currentAudit = Store.audits.find(a => a.id === auditId);
                await exportToExcel();
            }
        }

        // ========== ADMIN PANEL ==========

        // ========== EXCEL REPORT ==========
        async function generateExcelReport() {
            const container = document.getElementById('excelReportContainer');
            const statusFilter = document.getElementById('reportFilterStatus')?.value || 'all';
            const typeFilter = document.getElementById('reportFilterType')?.value || 'all';
            const searchQuery = document.getElementById('reportSearchTenant')?.value || '';

            showLoading();
            try {
                const report = await ApiClient.reports.getLatest(statusFilter, typeFilter, searchQuery);
                Store.excelReportData = report;
                renderExcelTable(report.rows, report.summary);
            } catch (err) {
                container.innerHTML = `
                        <div class="excel-empty-report">
                            <div class="icon">⚠️</div>
                            <h3>Gagal memuat data</h3>
                            <p>${err.message}</p>
                        </div>`;
            } finally {
                hideLoading();
            }
        }

        // rows: ExcelReportRow[], summary: ExcelReportSummary (from API)
        function renderExcelTable(rows, summary) {
            const container = document.getElementById('excelReportContainer');

            if (!rows || rows.length === 0) {
                container.innerHTML = `
                        <div class="excel-empty-report">
                            <div class="icon">📊</div>
                            <h3>Belum ada data audit</h3>
                            <p>Silakan lakukan audit terlebih dahulu untuk melihat reporting</p>
                        </div>`;
                return;
            }

            const avg = summary?.averagePassRate ?? 0;
            let html = `
                    <div class="excel-toolbar">
                        <div class="excel-toolbar-title"><span>📊</span><span>Hasil Audit Terakhir per Tenant</span></div>
                        <div class="excel-toolbar-actions">
                            <button class="excel-btn filter" onclick="resetExcelFilters()"><span>🔄</span> Reset Filter</button>
                            <button class="excel-btn export" onclick="exportExcelReport()"><span>📥</span> Export CSV</button>
                        </div>
                    </div>
                    <div class="excel-table-wrapper">
                        <table class="excel-table">
                            <thead><tr>
                                <th>No</th><th>Tenant</th><th>Type</th><th>Tanggal Audit</th>
                                <th>PIC</th><th>Status</th><th>Pass Rate</th>
                                <th>Total Items</th><th>Pass</th><th>Fail</th><th>Catatan Penting</th>
                            </tr></thead>
                            <tbody>`;

            rows.forEach(row => {
                const rc = row.passRate >= 90 ? 'good' : row.passRate >= 70 ? 'warning' : 'bad';
                html += `
                        <tr>
                            <td>${row.no}</td>
                            <td class="excel-cell-tenant">${row.tenantName}</td>
                            <td><span class="excel-cell-type ${row.usesGas ? 'gas' : 'non-gas'}">${row.usesGas ? '🔥 Gas' : '⚡ Non-Gas'}</span></td>
                            <td class="excel-cell-date">${new Date(row.date).toLocaleDateString('id-ID')}</td>
                            <td class="excel-cell-pic">${row.picName}</td>
                            <td><span class="excel-cell-status ${row.status.toLowerCase()}">${row.status === 'Completed' ? '✅' : '📝'} ${row.status}</span></td>
                            <td>
                                <div class="excel-progress-bar"><div class="excel-progress-fill ${rc}" style="width:${row.passRate}%"></div></div>
                                <span class="excel-cell-rate ${rc}">${row.passRate}%</span>
                            </td>
                            <td class="excel-cell-items">${row.totalItems}</td>
                            <td class="excel-cell-items">${row.passItems}</td>
                            <td class="excel-cell-fail">${row.failItems}</td>
                            <td class="excel-cell-note" title="${row.failNotes}">${row.failNotes || '-'}</td>
                        </tr>`;
            });

            const avgRc = avg >= 90 ? 'good' : avg >= 70 ? 'warning' : 'bad';
            html += `
                            <tr class="excel-summary-row">
                                <td colspan="6" style="text-align:right;">TOTAL / RATA-RATA</td>
                                <td>
                                    <div class="excel-progress-bar"><div class="excel-progress-fill ${avgRc}" style="width:${avg}%"></div></div>
                                    <span class="excel-cell-rate ${avgRc}">${avg}%</span>
                                </td>
                                <td>${summary?.totalItems ?? 0}</td>
                                <td>${summary?.totalPass ?? 0}</td>
                                <td class="excel-cell-fail">${summary?.totalFail ?? 0}</td>
                                <td>${summary?.tenantCount ?? rows.length} Tenant</td>
                            </tr>
                            </tbody></table></div>`;

            container.innerHTML = html;
        }

        function filterExcelReport() {
            // Re-fetch from API with new filter params
            generateExcelReport();
        }

        function resetExcelFilters() {
            document.getElementById('reportFilterStatus').value = 'all';
            document.getElementById('reportFilterType').value = 'all';
            document.getElementById('reportSearchTenant').value = '';
            generateExcelReport();
        }

        async function exportExcelReport() {
            showLoading();
            try {
                const statusFilter = document.getElementById('reportFilterStatus').value;
                const typeFilter = document.getElementById('reportFilterType').value;
                const searchQuery = document.getElementById('reportSearchTenant').value;

                const blob = await ApiClient.reports.exportExcel(statusFilter, typeFilter, searchQuery);
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `Report_Audit_Hygiene_${new Date().toISOString().split('T')[0]}.csv`;
                a.click();
                URL.revokeObjectURL(url);
                showToast('Report Excel berhasil diexport! 📊');
            } catch (err) {
                showToast('Gagal export: ' + err.message, 'error');
            } finally {
                hideLoading();
            }
        }

        function _adminGuard() {
            const role = Store.currentUser?.role ?? '';
            if (!['admin', 'Admin', 'SuperAdmin'].includes(role)) {
                showToast('Akses ditolak! Hanya admin.', 'error');
                navigateTo('dashboard');
                return false;
            }
            return true;
        }
        function initAdminUsers()     { if (_adminGuard()) renderUsers(); }
        function initAdminTemplates() { if (_adminGuard()) renderTemplates(); }
        function initAdminTenants()   { if (_adminGuard()) renderTenants(); }
        function initAdminReports()   { if (_adminGuard()) generateExcelReport(); }

        // ── Render Users ─────────────────────────────────────────────────────
        async function renderUsers() {
            const tbody = document.getElementById('usersTable');
            tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;padding:20px;color:#a5aeb7;">Memuat data...</td></tr>';
            try {
                const users = await ApiClient.users.getAll();
                if (!users.length) {
                    tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;padding:20px;color:#a5aeb7;">Belum ada data user.</td></tr>';
                    return;
                }
                tbody.innerHTML = users.map(u => `
                    <tr>
                        <td>${u.username}</td>
                        <td>${u.name}</td>
                        <td><span class="tenant-type ${u.role === 'Admin' || u.role === 'SuperAdmin' ? 'gas' : 'non-gas'}">${u.role}</span>
                            ${!u.isActive ? '<span style="font-size:11px;color:var(--danger);margin-left:4px;">(Non-aktif)</span>' : ''}</td>
                        <td style="white-space:nowrap;">
                            <button class="btn btn-sm" style="padding:4px 10px;margin-right:4px;" onclick="editUser(${u.id})">Edit</button>
                            <button class="btn btn-sm" style="padding:4px 10px;background:rgba(255,62,29,.1);color:var(--danger);border:none;" onclick="deleteUser(${u.id})">Hapus</button>
                        </td>
                    </tr>`).join('');
            } catch (err) {
                tbody.innerHTML = `<tr><td colspan="4" style="text-align:center;padding:20px;color:var(--danger);">Gagal memuat: ${err.message}</td></tr>`;
            }
        }

        // ── Render Templates ──────────────────────────────────────────────────
        async function renderTemplates() {
            const tbody = document.getElementById('templatesTable');
            tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;padding:20px;color:#a5aeb7;">Memuat data...</td></tr>';
            try {
                const templates = await ApiClient.templates.getAll();
                if (!templates.length) {
                    tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;padding:20px;color:#a5aeb7;">Belum ada template.</td></tr>';
                    return;
                }
                tbody.innerHTML = templates.map(t => `
                    <tr>
                        <td>${t.category}</td>
                        <td>${t.name}</td>
                        <td>${t.requiresGas ? '<span class="tenant-type gas">Ya</span>' : '<span style="color:#a5aeb7;font-size:.8125rem;">Tidak</span>'}</td>
                        <td style="white-space:nowrap;">
                            <button class="btn btn-sm" style="padding:4px 10px;margin-right:4px;" onclick="editTemplate(${t.id})">Edit</button>
                            <button class="btn btn-sm" style="padding:4px 10px;background:rgba(255,62,29,.1);color:var(--danger);border:none;" onclick="deleteTemplate(${t.id})">Hapus</button>
                        </td>
                    </tr>`).join('');
            } catch (err) {
                tbody.innerHTML = `<tr><td colspan="4" style="text-align:center;padding:20px;color:var(--danger);">Gagal memuat: ${err.message}</td></tr>`;
            }
        }

        // ── Render Tenants ────────────────────────────────────────────────────
        function renderTenants() {
            const tbody = document.getElementById('tenantsTable');
            if (!Store.tenants.length) {
                tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;padding:20px;color:#a5aeb7;">Belum ada data tenant.</td></tr>';
                return;
            }
            tbody.innerHTML = Store.tenants.map(t => `
                <tr>
                    <td>${t.name}</td>
                    <td>${t.floor ? `<span style="font-size:.8125rem;color:#566a7f;">${t.floor}</span>` : '<span style="color:#a5aeb7;font-size:.8125rem;">-</span>'}</td>
                    <td><span class="tenant-type ${t.usesGas ? 'gas' : 'non-gas'}">${t.usesGas ? 'Gas' : 'Non-Gas'}</span></td>
                    <td style="white-space:nowrap;">
                        <button class="btn btn-sm" style="padding:4px 10px;margin-right:4px;" onclick="editTenant(${t.id})">Edit</button>
                        <button class="btn btn-sm" style="padding:4px 10px;background:rgba(255,62,29,.1);color:var(--danger);border:none;" onclick="deleteTenant(${t.id})">Hapus</button>
                    </td>
                </tr>`).join('');
        }

        // ── Modal open helpers ────────────────────────────────────────────────
        function showUserModal() {
            document.getElementById('userModalTitle').textContent = 'Tambah User';
            document.getElementById('editUserId').value = '';
            document.getElementById('newUsername').disabled = false;
            document.getElementById('newPassword').required = true;
            document.getElementById('passwordHint').textContent = '';
            document.getElementById('activeGroup').style.display = 'none';
            document.getElementById('userModal').querySelector('form').reset();
            document.getElementById('userModal').classList.add('active');
        }

        function editUser(id) {
            ApiClient.users.getAll().then(users => {
                const u = users.find(x => x.id === id);
                if (!u) return;
                document.getElementById('userModalTitle').textContent = 'Edit User';
                document.getElementById('editUserId').value = u.id;
                document.getElementById('newUsername').value = u.username;
                document.getElementById('newUsername').disabled = true;
                document.getElementById('newFullName').value = u.name;
                document.getElementById('newRole').value = u.role;
                document.getElementById('newPassword').value = '';
                document.getElementById('newPassword').required = false;
                document.getElementById('passwordHint').textContent = '(kosongkan jika tidak diubah)';
                document.getElementById('editUserActive').checked = u.isActive;
                document.getElementById('activeGroup').style.display = 'block';
                document.getElementById('userModal').classList.add('active');
            }).catch(err => showToast(err.message, 'error'));
        }

        function showTemplateModal() {
            document.getElementById('templateModalTitle').textContent = 'Tambah Template';
            document.getElementById('editTemplateId').value = '';
            document.getElementById('templateModal').querySelector('form').reset();
            document.getElementById('templateModal').classList.add('active');
        }

        function editTemplate(id) {
            ApiClient.templates.getAll().then(templates => {
                const t = templates.find(x => x.id === id);
                if (!t) return;
                document.getElementById('templateModalTitle').textContent = 'Edit Template';
                document.getElementById('editTemplateId').value = t.id;
                document.getElementById('newCategory').value = t.category;
                document.getElementById('newActivity').value = t.name;
                document.getElementById('newDisplayOrder').value = t.displayOrder;
                document.getElementById('newGasOnly').checked = t.requiresGas;
                document.getElementById('templateModal').classList.add('active');
            }).catch(err => showToast(err.message, 'error'));
        }

        function showTenantModal() {
            document.getElementById('tenantModalTitle').textContent = 'Tambah Tenant';
            document.getElementById('editTenantId').value = '';
            document.getElementById('tenantModal').querySelector('form').reset();
            document.getElementById('tenantModal').classList.add('active');
        }

        function editTenant(id) {
            const t = Store.tenants.find(x => x.id === id);
            if (!t) return;
            document.getElementById('tenantModalTitle').textContent = 'Edit Tenant';
            document.getElementById('editTenantId').value = t.id;
            document.getElementById('newTenantName').value = t.name;
            document.getElementById('newTenantFloor').value = t.floor || '';
            document.getElementById('newTenantCategory').value = t.category || '';
            document.getElementById('newTenantGas').checked = t.usesGas;
            document.getElementById('tenantModal').classList.add('active');
        }

        function closeModal(modalId) {
            document.getElementById(modalId).classList.remove('active');
        }

        // ── Save functions ────────────────────────────────────────────────────
        async function saveUser(e) {
            e.preventDefault();
            const id       = document.getElementById('editUserId').value;
            const name     = document.getElementById('newFullName').value;
            const role     = document.getElementById('newRole').value;
            const password = document.getElementById('newPassword').value;
            const isActive = document.getElementById('editUserActive').checked;
            showLoading();
            try {
                if (id) {
                    const upd = { name, role, isActive };
                    if (password) upd.password = password;
                    await ApiClient.users.update(parseInt(id), upd);
                    showToast('User berhasil diperbarui!');
                } else {
                    const username = document.getElementById('newUsername').value;
                    await ApiClient.users.create({ username, password, name, role });
                    showToast('User berhasil ditambahkan!');
                }
                closeModal('userModal');
                renderUsers();
            } catch (err) {
                showToast(err.message, 'error');
            } finally { hideLoading(); }
        }

        async function saveTemplate(e) {
            e.preventDefault();
            const id           = document.getElementById('editTemplateId').value;
            const category     = document.getElementById('newCategory').value;
            const name         = document.getElementById('newActivity').value;
            const requiresGas  = document.getElementById('newGasOnly').checked;
            const displayOrder = parseInt(document.getElementById('newDisplayOrder').value) || 0;
            showLoading();
            try {
                if (id) {
                    await ApiClient.templates.update(parseInt(id), { category, name, requiresGas, displayOrder });
                    showToast('Template berhasil diperbarui!');
                } else {
                    await ApiClient.templates.create({ category, name, requiresGas, displayOrder });
                    showToast('Template berhasil ditambahkan!');
                }
                closeModal('templateModal');
                renderTemplates();
            } catch (err) {
                showToast(err.message, 'error');
            } finally { hideLoading(); }
        }

        async function saveTenant(e) {
            e.preventDefault();
            const id       = document.getElementById('editTenantId').value;
            const name     = document.getElementById('newTenantName').value;
            const usesGas  = document.getElementById('newTenantGas').checked;
            const floor    = document.getElementById('newTenantFloor').value || null;
            const category = document.getElementById('newTenantCategory').value || null;
            showLoading();
            try {
                if (id) {
                    await ApiClient.tenants.update(parseInt(id), { name, usesGas, floor, category });
                    showToast('Tenant berhasil diperbarui!');
                } else {
                    await ApiClient.tenants.create({ name, usesGas, floor, category });
                    showToast('Tenant berhasil ditambahkan!');
                }
                // Refresh Store.tenants
                Store.tenants = await ApiClient.tenants.getAll();
                closeModal('tenantModal');
                renderTenants();
                renderTenantOptions('');
            } catch (err) {
                showToast(err.message, 'error');
            } finally { hideLoading(); }
        }

        // ── Delete functions ──────────────────────────────────────────────────
        async function deleteUser(id) {
            if (!confirm('Nonaktifkan user ini?')) return;
            showLoading();
            try {
                await ApiClient.users.delete(id);
                showToast('User dinonaktifkan.');
                renderUsers();
            } catch (err) {
                showToast(err.message, 'error');
            } finally { hideLoading(); }
        }

        async function deleteTemplate(id) {
            if (!confirm('Hapus template ini?')) return;
            showLoading();
            try {
                await ApiClient.templates.delete(id);
                showToast('Template dihapus.');
                renderTemplates();
            } catch (err) {
                showToast(err.message, 'error');
            } finally { hideLoading(); }
        }

        async function deleteTenant(id) {
            if (!confirm('Hapus tenant ini?')) return;
            showLoading();
            try {
                await ApiClient.tenants.delete(id);
                Store.tenants = await ApiClient.tenants.getAll();
                showToast('Tenant dihapus.');
                renderTenants();
                renderTenantOptions('');
            } catch (err) {
                showToast(err.message, 'error');
            } finally { hideLoading(); }
        }

        // ========== DEMO DATA ==========
        function loadDemoData() {
            // Create a sample completed audit
            const demoAudit = {
                id: 'AUD-DEMO-001',
                date: '2026-05-10',
                tenantId: 1,
                tenantName: 'Teras by Plataran',
                picId: 2,
                picName: 'Budi Santoso',
                isGas: true,
                status: 'COMPLETED',
                createdAt: '2026-05-10T08:00:00Z',
                completedAt: '2026-05-10T09:30:00Z',
                items: Store.checklistTemplates.filter(t => !t.requiresGas || true).map((t, idx) => ({
                    templateId: t.id,
                    category: t.category,
                    name: t.name,
                    status: idx % 5 === 0 ? 'FAIL' : 'PASS',
                    note: idx % 5 === 0 ? 'Perlu perhatian khusus' : '',
                    photos: []
                })),
                categories: {}
            };

            // Recalculate categories
            const cats = {};
            demoAudit.items.forEach(item => {
                if (!cats[item.category]) cats[item.category] = [];
                cats[item.category].push({ id: item.templateId });
            });
            demoAudit.categories = cats;

            Store.audits.push(demoAudit);
        }

        // ─── Server status indicator ──────────────────────────────────────────
        async function checkServerStatus() {
            const dot = document.getElementById('serverStatusDot');
            const text = document.getElementById('serverStatusText');
            if (!dot || !text) return;

            const isFile = location.protocol === 'file:';
            if (isFile) {
                dot.style.background = '#f59e0b';
                text.textContent = `Buka via web server, bukan file://  (gunakan: npx serve FrontEnd)`;
                document.getElementById('serverStatus').style.background = '#fef3c7';
                document.getElementById('serverStatus').style.color = '#92400e';
                return;
            }

            const ok = await ApiClient.ping();
            if (ok) {
                dot.style.background = '#22c55e';
                text.textContent = `Server terhubung`;
                document.getElementById('serverStatus').style.background = '#f0fdf4';
                document.getElementById('serverStatus').style.color = '#166534';
            } else {
                dot.style.background = '#ef4444';
                text.textContent = `Server tidak ditemukan`;
                document.getElementById('serverStatus').style.background = '#fef2f2';
                document.getElementById('serverStatus').style.color = '#991b1b';
            }
        }

        // ========== SIDEBAR TOGGLE ==========
        function toggleMenu() {
            const menu = document.getElementById('layout-menu');
            const overlay = document.getElementById('layoutOverlay');
            if (menu) menu.classList.toggle('open');
            if (overlay) overlay.classList.toggle('active');
        }

        function closeMenu() {
            const menu = document.getElementById('layout-menu');
            const overlay = document.getElementById('layoutOverlay');
            if (menu) menu.classList.remove('open');
            if (overlay) overlay.classList.remove('active');
        }

        function toggleSidebarCollapse() {
            const menu = document.getElementById('layout-menu');
            const page = document.querySelector('.layout-page');
            const icon = document.querySelector('#sidebarToggle i');
            if (!menu) return;
            const collapsed = menu.classList.toggle('menu-collapsed');
            if (page) page.style.marginLeft = collapsed ? '68px' : 'var(--menu-width)';
            if (icon) icon.style.transform = collapsed ? 'rotate(180deg)' : '';
            localStorage.setItem('sidebarCollapsed', collapsed ? '1' : '0');
        }

        function initSidebarState() {
            if (localStorage.getItem('sidebarCollapsed') === '1') {
                const menu = document.getElementById('layout-menu');
                const page = document.querySelector('.layout-page');
                const icon = document.querySelector('#sidebarToggle i');
                if (menu) menu.classList.add('menu-collapsed');
                if (page) page.style.marginLeft = '68px';
                if (icon) icon.style.transform = 'rotate(180deg)';
            }
        }

                // Initialize
        document.addEventListener('DOMContentLoaded', async () => {
            await initIndexedDB().catch(console.error);
            SyncManager.init();
            NotificationManager.init();
            initSidebarState();

            // Close searchable dropdown when clicking outside
            document.addEventListener('click', function(e) {
                if (!e.target.closest('#tenantDropdown')) closeTenantDropdown();
            });

            // Check backend reachability and show status on login page
            checkServerStatus();

            // Restore session if JWT is still valid
            if (ApiClient.isAuthenticated()) {
                const saved = ApiClient.getUser();
                Store.currentUser = { id: saved.id, name: saved.name, role: saved.role };
                await _applySession(Store.currentUser);
            }
        });

        // Service Worker for PWA
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.register('data:text/javascript,' + encodeURIComponent(`
                    self.addEventListener('install', e => self.skipWaiting());
                    self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));
                    self.addEventListener('fetch', e => e.respondWith(fetch(e.request).catch(() => new Response('Offline'))));

                    // Push notification handler
                    self.addEventListener('push', event => {
                        const data = event.data.json();
                        event.waitUntil(
                            self.registration.showNotification(data.title, {
                                body: data.body,
                                icon: data.icon || '/icon-192x192.png',
                                badge: '/badge-72x72.png',
                                tag: data.tag || 'default'
                            })
                        );
                    });

                    // Notification click handler
                    self.addEventListener('notificationclick', event => {
                        event.notification.close();
                        event.waitUntil(
                            clients.openWindow('/')
                        );
                    });
                `)).catch(() => { });
        }
if ('serviceWorker' in navigator) {
            navigator.serviceWorker.register('data:text/javascript,' + encodeURIComponent(`
                self.addEventListener('install', e => self.skipWaiting());
                self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));
                self.addEventListener('fetch', e => e.respondWith(fetch(e.request).catch(() => new Response('Offline'))));
                self.addEventListener('push', event => {
                    const data = event.data.json();
                    event.waitUntil(self.registration.showNotification(data.title, {
                        body: data.body, icon: data.icon || '/icon-192x192.png',
                        badge: '/badge-72x72.png', tag: data.tag || 'default'
                    }));
                });
                self.addEventListener('notificationclick', event => {
                    event.notification.close();
                    event.waitUntil(clients.openWindow('/'));
                });
            `)).catch(() => {});
        }
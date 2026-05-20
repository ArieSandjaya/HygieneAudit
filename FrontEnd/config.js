/**
 * Hygiene Audit App - Backend Configuration
 *
 * Cara penggunaan:
 *   Tambahkan baris berikut di hygiene_audit_app.html sebelum script utama:
 *   <script src="config.js"></script>
 *
 * Untuk production, ganti API_BASE_URL dengan URL server yang sebenarnya.
 */

const AppConfig = {

    // ─── Base URL Backend ────────────────────────────────────────────────────
    API_BASE_URL: 'http://localhost:5000',   // development (HTTP)
    // API_BASE_URL: 'https://localhost:7000',  // development (HTTPS)
    // API_BASE_URL: 'https://api.yourdomain.com', // production

    // ─── Endpoints ───────────────────────────────────────────────────────────
    ENDPOINTS: {
        // Auth
        LOGIN:                  '/api/auth/login',

        // Audits
        AUDITS:                 '/api/audits',
        AUDIT_BY_ID:            (id)           => `/api/audits/${id}`,
        AUDIT_ITEM:             (id, tplId)    => `/api/audits/${id}/items/${tplId}`,
        AUDIT_SUBMIT:           (id)           => `/api/audits/${id}/submit`,
        AUDIT_DRAFT:            (id)           => `/api/audits/${id}/draft`,

        // Tenants
        TENANTS:                '/api/tenants',
        TENANT_HISTORY:         (id)           => `/api/tenants/${id}/history`,

        // Reports (Admin only)
        REPORT_LATEST:          '/api/reports/latest-per-tenant',
        REPORT_EXPORT_EXCEL:    '/api/reports/export-excel',

        // Sync
        SYNC:                   '/api/sync',
    },

    // ─── JWT ─────────────────────────────────────────────────────────────────
    TOKEN_STORAGE_KEY:  'hygiene_audit_token',
    USER_STORAGE_KEY:   'hygiene_audit_user',

    // ─── Request ─────────────────────────────────────────────────────────────
    REQUEST_TIMEOUT_MS: 30_000,    // 30 detik

    // ─── Offline Sync ────────────────────────────────────────────────────────
    SYNC_RETRY_MAX:     3,
    SYNC_RETRY_DELAY_MS: 2_000,
};

// ─── API Client ───────────────────────────────────────────────────────────────

const ApiClient = {

    _getToken() {
        return localStorage.getItem(AppConfig.TOKEN_STORAGE_KEY);
    },

    _buildHeaders(extra = {}) {
        const headers = {
            'Content-Type': 'application/json',
            ...extra,
        };
        const token = this._getToken();
        if (token) {
            headers['Authorization'] = `Bearer ${token}`;
        }
        return headers;
    },

    async _request(method, endpoint, body = null, params = null) {
        let url = AppConfig.API_BASE_URL + endpoint;

        if (params) {
            const qs = new URLSearchParams(
                Object.fromEntries(
                    Object.entries(params).filter(([, v]) => v !== null && v !== undefined)
                )
            ).toString();
            if (qs) url += `?${qs}`;
        }

        const controller = new AbortController();
        const timeoutId = setTimeout(
            () => controller.abort(),
            AppConfig.REQUEST_TIMEOUT_MS
        );

        try {
            const response = await fetch(url, {
                method,
                headers: this._buildHeaders(),
                body: body ? JSON.stringify(body) : null,
                signal: controller.signal,
            });

            clearTimeout(timeoutId);

            if (response.status === 401) {
                ApiClient.clearSession();
                throw new Error('Sesi berakhir, silakan login kembali.');
            }

            if (!response.ok) {
                const errText = await response.text();
                let errMsg;
                try {
                    const errJson = JSON.parse(errText);
                    errMsg = errJson.message || errJson.title || errText;
                } catch {
                    errMsg = errText || `HTTP ${response.status}`;
                }
                throw new Error(errMsg);
            }

            const contentType = response.headers.get('content-type') || '';
            if (contentType.includes('application/json')) {
                return await response.json();
            }

            return await response.blob();

        } catch (err) {
            clearTimeout(timeoutId);
            if (err.name === 'AbortError') {
                throw new Error('Request timeout. Periksa koneksi Anda.');
            }
            // TypeError = network-level failure: server not running, CORS preflight blocked,
            // or HTML opened via file:// (browsers block fetch from null origin to localhost).
            if (err instanceof TypeError) {
                const isFileProtocol = location.protocol === 'file:';
                if (isFileProtocol) {
                    throw new Error(
                        'HTML dibuka via file://. Buka menggunakan web server lokal ' +
                        '(contoh: "npx serve FrontEnd" atau Live Server di VS Code).'
                    );
                }
                throw new Error(
                    `Tidak dapat terhubung ke backend (${AppConfig.API_BASE_URL}). ` +
                    'Pastikan server sudah berjalan: cd BackEnd && dotnet run'
                );
            }
            throw err;
        }
    },

    // Quick reachability check — resolves true/false, never throws
    async ping() {
        try {
            const ctrl = new AbortController();
            setTimeout(() => ctrl.abort(), 5000);
            const res = await fetch(AppConfig.API_BASE_URL + '/api/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username: '', password: '' }),
                signal: ctrl.signal,
            });
            // Any HTTP response (even 401/400) means the server is up
            return res.status !== 0;
        } catch {
            return false;
        }
    },

    get:    (endpoint, params)  => ApiClient._request('GET',    endpoint, null, params),
    post:   (endpoint, body)    => ApiClient._request('POST',   endpoint, body),
    put:    (endpoint, body)    => ApiClient._request('PUT',    endpoint, body),
    delete: (endpoint)          => ApiClient._request('DELETE', endpoint),

    // ─── Auth helpers ──────────────────────────────────────────────────────

    saveSession(authResponse) {
        localStorage.setItem(AppConfig.TOKEN_STORAGE_KEY, authResponse.token);

        // Decode user ID from JWT payload (NameIdentifier claim)
        let userId = null;
        try {
            const payload = JSON.parse(atob(authResponse.token.split('.')[1]));
            const idClaim = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
            userId = payload[idClaim] ? parseInt(payload[idClaim]) : null;
        } catch {}

        localStorage.setItem(AppConfig.USER_STORAGE_KEY, JSON.stringify({
            id:        userId,
            name:      authResponse.name,
            role:      authResponse.role,
            expiresAt: authResponse.expiresAt,
        }));
    },

    clearSession() {
        localStorage.removeItem(AppConfig.TOKEN_STORAGE_KEY);
        localStorage.removeItem(AppConfig.USER_STORAGE_KEY);
    },

    getUser() {
        const raw = localStorage.getItem(AppConfig.USER_STORAGE_KEY);
        return raw ? JSON.parse(raw) : null;
    },

    isAuthenticated() {
        const user = this.getUser();
        if (!user || !user.expiresAt) return false;
        return new Date(user.expiresAt) > new Date();
    },

    // ─── Convenience methods ──────────────────────────────────────────────

    auth: {
        login: (username, password) =>
            ApiClient.post(AppConfig.ENDPOINTS.LOGIN, { username, password }),
    },

    audits: {
        getAll:     ()              => ApiClient.get(AppConfig.ENDPOINTS.AUDITS),
        create:     (data)          => ApiClient.post(AppConfig.ENDPOINTS.AUDITS, data),
        getById:    (id)            => ApiClient.get(AppConfig.ENDPOINTS.AUDIT_BY_ID(id)),
        updateItem: (id, tplId, data) =>
            ApiClient.put(AppConfig.ENDPOINTS.AUDIT_ITEM(id, tplId), data),
        submit:     (id)            => ApiClient.post(AppConfig.ENDPOINTS.AUDIT_SUBMIT(id)),
        saveDraft:  (id)            => ApiClient.post(AppConfig.ENDPOINTS.AUDIT_DRAFT(id)),
    },

    tenants: {
        getAll:     ()   => ApiClient.get(AppConfig.ENDPOINTS.TENANTS),
        getHistory: (id) => ApiClient.get(AppConfig.ENDPOINTS.TENANT_HISTORY(id)),
    },

    reports: {
        getLatest:  (status, type, search) =>
            ApiClient.get(AppConfig.ENDPOINTS.REPORT_LATEST, { status, type, search }),
        exportExcel: (status, type, search) =>
            ApiClient.get(AppConfig.ENDPOINTS.REPORT_EXPORT_EXCEL, { status, type, search }),
    },

    sync: {
        push: (items) => ApiClient.post(AppConfig.ENDPOINTS.SYNC, items),
    },
};

// Audits list viewmodel
function AuditsViewModel(currentUserId, isAdmin) {
    var self = this;
    self.audits = ko.observableArray([]);
    self.tenants = ko.observableArray([]);
    self.users = ko.observableArray([]);
    self.showForm = ko.observable(false);
    self.activeTab = ko.observable('all'); // 'all' | 'active' | 'history'

    self.newAudit = {
        tenantId: ko.observable(null),
        auditDate: ko.observable(new Date().toISOString().split('T')[0]),
        includeGas: ko.observable('false'),
        picId: ko.observable(null)
    };

    // Searchable tenant dropdown
    self.tenantSearch = ko.observable('');
    self.filteredTenants = ko.computed(function () {
        var q = self.tenantSearch().toLowerCase();
        return q ? self.tenants().filter(function (t) { return t.name.toLowerCase().indexOf(q) >= 0; })
                 : self.tenants();
    });
    self.selectedTenantName = ko.computed(function () {
        var id = self.newAudit.tenantId();
        var t = ko.utils.arrayFirst(self.tenants(), function (t) { return t.id == id; });
        return t ? t.name : null;
    });

    // Tenant history preview
    self.tenantHistoryVisible = ko.observable(false);
    self.tenantHistoryAudits = ko.observableArray([]);
    self.tenantHistorySubtitle = ko.computed(function () {
        return self.tenantHistoryAudits().length + ' audit';
    });
    self.tenantTotalAudits = ko.computed(function () { return self.tenantHistoryAudits().length; });
    self.tenantAvgPass = ko.computed(function () {
        var h = self.tenantHistoryAudits();
        if (!h.length) return 0;
        var sum = h.reduce(function (n, a) {
            return n + (a.totalItems > 0 ? a.passCount / a.totalItems : 0);
        }, 0);
        return Math.round(sum / h.length * 100);
    });
    self.tenantLastAuditDays = ko.computed(function () {
        var h = self.tenantHistoryAudits();
        if (!h.length) return '-';
        var d = Math.floor((new Date() - new Date(h[0].date)) / 86400000);
        return d === 0 ? 'Hari ini' : d + ' hari lalu';
    });
    self.tenantTrendBars = ko.computed(function () {
        return self.tenantHistoryAudits().slice(0, 6).reverse().map(function (a) {
            var rate = a.totalItems > 0 ? Math.round(a.passCount / a.totalItems * 100) : 0;
            var dt = new Date(a.date);
            return {
                height: Math.max(rate * 0.48, 4) + 'px',
                color: rate >= 100 ? '#22c55e' : '#ef4444',
                label: dt.getDate() + '/' + (dt.getMonth() + 1)
            };
        });
    });
    self.passRateLabel = function (a) {
        return a.totalItems > 0 ? Math.round(a.passCount / a.totalItems * 100) + '%' : '-';
    };
    self.passRateCss = function (a) {
        var rate = a.totalItems > 0 ? a.passCount / a.totalItems * 100 : 0;
        return rate >= 100 ? 'bg-label-success' : 'bg-label-danger';
    };

    // Hanya PIC yang melakukan audit yang boleh menghapus (admin pun tidak).
    self.canDeleteAudit = function (a) {
        return a.picId === currentUserId;
    };
    // Delete a DRAFT audit from the list. Lives inside the row's <a>, so we stop
    // the click from navigating to the detail page first.
    self.deleteAudit = function (a, event) {
        if (event) { event.preventDefault(); event.stopPropagation(); }
        if (a.status !== 'DRAFT') return;
        showConfirm({
            title: 'Hapus Draft Audit',
            message: 'Hapus draft audit "' + (a.tenantName || '') + '"?\nTindakan ini tidak dapat dibatalkan.',
            confirmText: 'Hapus',
            onConfirm: function () {
                $.ajax({ url: '/api/audits/' + a.id, type: 'DELETE' })
                    .done(function () {
                        self.audits.remove(a);
                        showToast('Draft audit dihapus.');
                    })
                    .fail(function (xhr) {
                        showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal menghapus audit.', 'error');
                    });
            }
        });
    };

    self.selectTenant = function (tenant) {
        self.newAudit.tenantId(tenant.id);
        self.tenantSearch('');
        var history = self.audits().filter(function (a) { return a.tenantId == tenant.id; })
                                   .sort(function (a, b) { return new Date(b.date) - new Date(a.date); });
        self.tenantHistoryAudits(history);
        self.tenantHistoryVisible(true);
    };

    self.filteredAudits = ko.computed(function () {
        var tab = self.activeTab();
        return self.audits().filter(function (a) {
            if (tab === 'active')  return a.status !== 'COMPLETED';
            if (tab === 'history') return a.status === 'COMPLETED';
            return true;
        });
    });
    self.ps = new PagedSorted(self.filteredAudits, 10);

    self.init = function () {
        $.getJSON('/api/audits').done(function (data) {
            data.forEach(function (audit) {
                // passCount/totalItems pre-computed by server — no need to iterate items client-side.
                audit.passRate = audit.totalItems > 0 ? audit.passCount / audit.totalItems : 0;
            });
            self.audits(data);
        }).fail(function () { showToast('Gagal memuat daftar audit.', 'error'); });

        $.getJSON('/api/tenants').done(function (d) { self.tenants(d); })
            .fail(function () { showToast('Gagal memuat daftar tenant.', 'error'); });
        $.getJSON('/api/users/auditors').done(function (d) {
            self.users(d);
            if (!isAdmin && currentUserId) {
                self.newAudit.picId(currentUserId);
            }
        }).fail(function (xhr) {
            showToast('Gagal memuat daftar auditor (status ' + xhr.status + ').', 'error');
        });
    };

    self.showCreateForm = function () { self.showForm(true); };
    self.cancelForm     = function () {
        self.showForm(false);
        self.tenantHistoryVisible(false);
        self.tenantHistoryAudits([]);
    };

    self.createAudit = function () {
        var data = {
            date:     self.newAudit.auditDate(),
            tenantId: parseInt(self.newAudit.tenantId(), 10) || 0,
            picId:    parseInt(self.newAudit.picId(), 10)    || 0,
            isGas:    self.newAudit.includeGas() === 'true'
        };
        $.ajax({ url: '/api/audits', type: 'POST', contentType: 'application/json', data: JSON.stringify(data) })
            .done(function (audit) { window.location.href = '/Audits/Detail/' + audit.id; })
            .fail(function (xhr)   { showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal membuat audit.', 'error'); });
    };

    self.init();
}

// Compress an image File to a JPEG Blob no larger than maxSide × maxSide px.
// Returns a Promise<Blob>. Falls back to the original File if Canvas is unavailable.
function compressImage(file, maxSide, quality) {
    return new Promise(function (resolve) {
        var objectUrl = URL.createObjectURL(file);
        var img = new Image();
        img.onload = function () {
            var w = img.naturalWidth  || img.width;
            var h = img.naturalHeight || img.height;
            if (w > maxSide || h > maxSide) {
                var ratio = Math.min(maxSide / w, maxSide / h);
                w = Math.round(w * ratio);
                h = Math.round(h * ratio);
            }
            var canvas = document.createElement('canvas');
            canvas.width  = w;
            canvas.height = h;
            canvas.getContext('2d').drawImage(img, 0, 0, w, h);
            URL.revokeObjectURL(objectUrl);
            canvas.toBlob(function (blob) {
                resolve(blob || file); // fall back to original if toBlob fails
            }, 'image/jpeg', quality);
        };
        img.onerror = function () {
            URL.revokeObjectURL(objectUrl);
            resolve(file); // fallback: send original without compression
        };
        img.src = objectUrl;
    });
}

// Audit detail viewmodel
function AuditDetailViewModel(auditId, currentUserId, isAdmin) {
    var self = this;
    self.audit      = ko.observable(null);
    self.categories = ko.observableArray([]);
    self.submitting = ko.observable(false);
    self.savingDraft = ko.observable(false);
    self.canEdit    = ko.observable(false); // resolved after audit loads

    // Flattened header props
    self.tenantName = ko.computed(function () { return self.audit() ? self.audit().tenantName : ''; });
    self.auditDate  = ko.computed(function () { return self.audit() ? self.audit().date : null; });
    self.picName    = ko.computed(function () { return self.audit() ? self.audit().picName : ''; });
    self.status     = ko.computed(function () { return self.audit() ? self.audit().status : ''; });
    self.isGas      = ko.computed(function () { return self.audit() ? self.audit().isGas : false; });
    // Hanya PIC yang melakukan audit yang boleh menghapus draft (admin pun tidak).
    self.canDelete  = ko.computed(function () {
        var a = self.audit();
        return self.status() === 'DRAFT' && a != null && a.picId === currentUserId;
    });

    self.passCount  = ko.computed(function () {
        return self.categories().reduce(function (n, cat) {
            return n + cat.items.filter(function (i) { return i.status() === 'PASS'; }).length;
        }, 0);
    });
    self.failCount  = ko.computed(function () {
        return self.categories().reduce(function (n, cat) {
            return n + cat.items.filter(function (i) { return i.status() === 'FAIL'; }).length;
        }, 0);
    });
    self.totalCount = ko.computed(function () {
        return self.categories().reduce(function (n, cat) { return n + cat.items.length; }, 0);
    });

    // Progress-bar style values (kept here so the view's data-bind stays a
    // simple single-line attribute — avoids VS's HTML-validator false positives).
    self.progressWidth = ko.computed(function () {
        var t = self.totalCount();
        return (t > 0 ? Math.round(self.passCount() / t * 100) : 0) + '%';
    });
    self.progressColor = ko.computed(function () {
        var t = self.totalCount();
        return (t > 0 && self.passCount() / t >= 0.7) ? '#22c55e' : '#ef4444';
    });

    self.init = function () {
        $.getJSON('/api/audits/' + auditId).done(function (audit) {
            self.audit(audit);
            self.canEdit(isAdmin || audit.picId === currentUserId);

            var grouped = {};
            (audit.items || []).forEach(function (item) {
                if (!grouped[item.category]) grouped[item.category] = [];
                item.status = ko.observable(item.status || '');
                item.note   = ko.observable(item.note   || '');
                item.photos = ko.observableArray(item.photos || []);
                grouped[item.category].push(item);
            });
            self.categories(Object.keys(grouped).map(function (k) {
                var catItems = grouped[k];
                var cat = { name: k, items: catItems, collapsed: ko.observable(false) };
                cat.checkedCount = ko.computed(function () {
                    return catItems.filter(function (i) { return i.status() !== ''; }).length;
                });
                return cat;
            }));
        }).fail(function () {
            showToast('Gagal memuat data audit. Silakan kembali dan coba lagi.', 'error');
        });
    };

    self.toggleCategory = function (cat) { cat.collapsed(!cat.collapsed()); };

    self.addPhoto = function (item, event) {
        var fileList = event.target.files;
        if (!fileList || !fileList.length) return;

        // Snapshot into a plain Array BEFORE resetting the input.
        // Browsers invalidate the live FileList when input.value is cleared;
        // a plain Array is unaffected by that reset.
        var files = Array.prototype.slice.call(fileList);
        event.target.value = ''; // reset so the same file can be picked again

        files.forEach(function (f) {
            compressImage(f, 1280, 0.82).then(function (blob) {
                var fd = new FormData();
                fd.append('photo', blob, 'photo.jpg');
                return $.ajax({
                    url: '/api/audits/' + auditId + '/items/' + item.templateId + '/photos',
                    type: 'POST',
                    data: fd,
                    processData: false,
                    contentType: false
                });
            }).then(function (result) {
                item.photos.push(result.url); // reference URL from server
            }).catch(function (err) {
                var msg = (err && err.responseJSON && err.responseJSON.message)
                    ? err.responseJSON.message
                    : 'Gagal mengunggah foto.';
                showToast(msg, 'error');
            });
        });
    };

    self.removePhoto = function (item, url) {
        item.photos.remove(url);
        self.onItemChange(item);
    };

    self.setItemStatus = function (item, status) {
        item.status(item.status() === status ? '' : status);
        self.onItemChange(item);
    };

    self.onItemChange = function (item) {
        if (!self.canEdit()) return;
        var data = { status: item.status(), note: item.note(), photos: item.photos() };
        $.ajax({
            url: '/api/audits/' + auditId + '/items/' + item.templateId,
            type: 'PUT', contentType: 'application/json', data: JSON.stringify(data)
        }).fail(function () { showToast('Gagal menyimpan perubahan item.', 'error'); });
    };

    self.exportPdf = function () {
        window.open('/Audits/PrintReport/' + auditId, '_blank');
    };

    self.saveDraft = function () {
        if (self.savingDraft() || !self.canEdit()) return;
        self.savingDraft(true);
        $.ajax({ url: '/api/audits/' + auditId + '/draft', type: 'POST' })
            .done(function () { showToast('Draft disimpan!'); })
            .fail(function () { showToast('Gagal menyimpan draft.', 'error'); })
            .always(function () { self.savingDraft(false); });
    };

    self.deleting = ko.observable(false);
    self.deleteAudit = function () {
        if (self.deleting() || !self.canDelete()) return;
        showConfirm({
            title: 'Hapus Draft Audit',
            message: 'Hapus draft audit ini?\nTindakan ini tidak dapat dibatalkan.',
            confirmText: 'Hapus',
            onConfirm: function () {
                self.deleting(true);
                $.ajax({ url: '/api/audits/' + auditId, type: 'DELETE' })
                    .done(function () {
                        showToast('Draft audit dihapus.');
                        setTimeout(function () { window.location.href = '/Audits'; }, 800);
                    })
                    .fail(function (xhr) {
                        self.deleting(false);
                        showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal menghapus audit.', 'error');
                    });
            }
        });
    };

    self.submitAudit = function () {
        if (self.submitting()) return;
        var unchecked = self.totalCount() - self.passCount() - self.failCount();
        if (unchecked > 0) {
            showToast(unchecked + ' item belum dicek!', 'error');
            return;
        }
        self.submitting(true);
        $.ajax({ url: '/api/audits/' + auditId + '/submit', type: 'POST' })
            .done(function () {
                showToast('Audit berhasil diselesaikan!');
                // Reset before navigating so a blocked redirect (beforeunload) doesn't lock the button.
                self.submitting(false);
                setTimeout(function () { window.location.href = '/Audits'; }, 1500);
            })
            .fail(function (xhr) {
                self.submitting(false);
                showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal mengirim audit.', 'error');
            });
    };

    self.init();
}

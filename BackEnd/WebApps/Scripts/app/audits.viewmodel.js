// Audits list viewmodel
function AuditsViewModel() {
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
            var items = a.items || [];
            var pass = items.filter(function (i) { return i.status === 'PASS'; }).length;
            return n + (items.length ? pass / items.length : 0);
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
            var items = a.items || [];
            var pass = items.filter(function (i) { return i.status === 'PASS'; }).length;
            var rate = items.length ? Math.round(pass / items.length * 100) : 0;
            var dt = new Date(a.date);
            return {
                height: Math.max(rate * 0.48, 4) + 'px',
                color: rate >= 70 ? '#22c55e' : '#ef4444',
                label: dt.getDate() + '/' + (dt.getMonth() + 1)
            };
        });
    });
    self.passRateLabel = function (a) {
        var items = a.items || [];
        var pass = items.filter(function (i) { return i.status === 'PASS'; }).length;
        return items.length ? Math.round(pass / items.length * 100) + '%' : '-';
    };
    self.passRateCss = function (a) {
        var items = a.items || [];
        var pass = items.filter(function (i) { return i.status === 'PASS'; }).length;
        var rate = items.length ? pass / items.length * 100 : 0;
        return rate >= 70 ? 'bg-label-success' : 'bg-label-danger';
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

    self.init = function () {
        $.getJSON('/api/audits').done(function (data) {
            data.forEach(function (audit) {
                var items = audit.items || [];
                var answered = items.filter(function (i) { return i.status; });
                var passed   = items.filter(function (i) { return i.status === 'PASS'; });
                audit.passRate = answered.length > 0 ? passed.length / answered.length : 0;
            });
            self.audits(data);
        }).fail(function () { showToast('Gagal memuat daftar audit.', 'error'); });

        $.getJSON('/api/tenants').done(function (d) { self.tenants(d); })
            .fail(function () { showToast('Gagal memuat daftar tenant.', 'error'); });
        $.getJSON('/api/users').done(function (d) { self.users(d); }).fail(function () { self.users([]); });
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
            tenantId: parseInt(self.newAudit.tenantId()) || 0,
            picId:    parseInt(self.newAudit.picId())    || 0,
            isGas:    self.newAudit.includeGas() === 'true'
        };
        $.ajax({ url: '/api/audits', type: 'POST', contentType: 'application/json', data: JSON.stringify(data) })
            .done(function (audit) { window.location.href = '/Audits/Detail/' + audit.id; })
            .fail(function (xhr)   { showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal membuat audit.', 'error'); });
    };

    self.init();
}

// Audit detail viewmodel
function AuditDetailViewModel(auditId) {
    var self = this;
    self.audit      = ko.observable(null);
    self.categories = ko.observableArray([]);

    // Flattened header props
    self.tenantName = ko.computed(function () { return self.audit() ? self.audit().tenantName : ''; });
    self.auditDate  = ko.computed(function () { return self.audit() ? self.audit().date : null; });
    self.picName    = ko.computed(function () { return self.audit() ? self.audit().picName : ''; });
    self.status     = ko.computed(function () { return self.audit() ? self.audit().status : ''; });
    self.isGas      = ko.computed(function () { return self.audit() ? self.audit().isGas : false; });

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

    self.init = function () {
        $.getJSON('/api/audits/' + auditId).done(function (audit) {
            self.audit(audit);

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
        var files = event.target.files;
        if (!files || !files.length) return;
        var reads = Array.prototype.map.call(files, function (f) {
            return new Promise(function (resolve) {
                var reader = new FileReader();
                reader.onload = function (e) { resolve(e.target.result); };
                reader.readAsDataURL(f);
            });
        });
        Promise.all(reads).then(function (dataUrls) {
            dataUrls.forEach(function (url) { item.photos.push(url); });
            self.onItemChange(item);
        });
        event.target.value = '';
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
        var data = { status: item.status(), note: item.note(), photos: item.photos() };
        $.ajax({
            url: '/api/audits/' + auditId + '/items/' + item.templateId,
            type: 'PUT', contentType: 'application/json', data: JSON.stringify(data)
        }).fail(function () { showToast('Gagal menyimpan perubahan item.', 'error'); });
    };

    self.saveDraft = function () {
        $.ajax({ url: '/api/audits/' + auditId + '/draft', type: 'POST' })
            .done(function () { showToast('Draft disimpan!'); })
            .fail(function () { showToast('Gagal menyimpan draft.', 'error'); });
    };

    self.submitAudit = function () {
        if (!confirm('Yakin ingin menyelesaikan audit ini?')) return;
        $.ajax({ url: '/api/audits/' + auditId + '/submit', type: 'POST' })
            .done(function () {
                showToast('Audit berhasil diselesaikan! 🎉');
                setTimeout(function () { window.location.href = '/Audits'; }, 1500);
            })
            .fail(function (xhr) { showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal.', 'error'); });
    };

    self.init();
}

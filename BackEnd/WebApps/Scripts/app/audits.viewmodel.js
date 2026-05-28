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
    self.selectTenant = function (tenant) {
        self.newAudit.tenantId(tenant.id);
        self.tenantSearch('');
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
        });

        $.getJSON('/api/tenants').done(function (d) { self.tenants(d); });
        $.getJSON('/api/users').done(function (d) { self.users(d); }).fail(function () { self.users([]); });
    };

    self.showCreateForm = function () { self.showForm(true); };
    self.cancelForm     = function () { self.showForm(false); };

    self.createAudit = function () {
        var data = {
            date:     self.newAudit.auditDate(),
            tenantId: parseInt(self.newAudit.tenantId()) || 0,
            picId:    parseInt(self.newAudit.picId())    || 0,
            isGas:    self.newAudit.includeGas() === 'true'
        };
        $.ajax({ url: '/api/audits', type: 'POST', contentType: 'application/json', data: JSON.stringify(data) })
            .done(function (audit) { window.location.href = '/Audits/Detail/' + audit.id; })
            .fail(function (xhr)   { alert((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal membuat audit.'); });
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
                return { name: k, items: grouped[k] };
            }));
        });
    };

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
        item.status(item.status() === status ? '' : status); // toggle off jika klik ulang
        self.onItemChange(item);
    };

    self.onItemChange = function (item) {
        var data = { status: item.status(), note: item.note(), photos: item.photos() };
        $.ajax({
            url: '/api/audits/' + auditId + '/items/' + item.templateId,
            type: 'PUT', contentType: 'application/json', data: JSON.stringify(data)
        });
    };

    self.saveDraft = function () {
        $.ajax({ url: '/api/audits/' + auditId + '/draft', type: 'POST' })
            .done(function () { alert('Draft disimpan.'); });
    };

    self.submitAudit = function () {
        if (!confirm('Yakin ingin menyelesaikan audit ini?')) return;
        $.ajax({ url: '/api/audits/' + auditId + '/submit', type: 'POST' })
            .done(function () {
                alert('Audit berhasil diselesaikan!');
                window.location.href = '/Audits';
            })
            .fail(function (xhr) { alert((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal.'); });
    };

    self.init();
}

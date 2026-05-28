// Audits list viewmodel
function AuditsViewModel() {
    var self = this;
    self.audits = ko.observableArray([]);
    self.tenants = ko.observableArray([]);
    self.users = ko.observableArray([]);
    self.showForm = ko.observable(false);
    self.form = {
        date: ko.observable(new Date().toISOString().split('T')[0]),
        tenantId: ko.observable(null),
        picId: ko.observable(null),
        isGas: ko.observable(false)
    };

    self.init = function () {
        $.when(
            $.getJSON('/api/audits'),
            $.getJSON('/api/tenants'),
            $.getJSON('/api/users')
        ).done(function (auditsRes, tenantsRes, usersRes) {
            self.audits(auditsRes[0]);
            self.tenants(tenantsRes[0]);
            self.users(usersRes[0]);
        }).fail(function (xhr) {
            if (xhr.status === 403) self.users([]);
            $.getJSON('/api/tenants').done(function (d) { self.tenants(d); });
        });
        $.getJSON('/api/audits').done(function (d) { self.audits(d); });
    };

    self.createAudit = function () { self.showForm(true); };
    self.cancelCreate = function () { self.showForm(false); };

    self.submitCreate = function () {
        var data = {
            date: self.form.date(),
            tenantId: parseInt(self.form.tenantId()),
            picId: parseInt(self.form.picId()),
            isGas: self.form.isGas()
        };
        $.ajax({ url: '/api/audits', type: 'POST', contentType: 'application/json', data: JSON.stringify(data) })
            .done(function (audit) {
                window.location.href = '/Audits/Detail?id=' + audit.id;
            })
            .fail(function (xhr) { alert(xhr.responseJSON && xhr.responseJSON.message || 'Gagal membuat audit.'); });
    };

    self.init();
}

// Audit detail viewmodel
function AuditDetailViewModel(auditId) {
    var self = this;
    self.audit = ko.observable(null);
    self.categories = ko.observableArray([]);

    self.init = function () {
        $.getJSON('/api/audits/' + auditId).done(function (audit) {
            self.audit(audit);
            // Group items by category
            var grouped = {};
            (audit.items || []).forEach(function (item) {
                if (!grouped[item.category]) grouped[item.category] = [];
                item.status = ko.observable(item.status || '');
                item.note = ko.observable(item.note || '');
                grouped[item.category].push(item);
            });
            var cats = Object.keys(grouped).map(function (k) {
                return { name: k, items: grouped[k] };
            });
            self.categories(cats);
        });
    };

    self.onItemChange = function (item) {
        var data = { status: item.status(), note: item.note(), photos: [] };
        $.ajax({
            url: '/api/audits/' + auditId + '/items/' + item.templateId,
            type: 'PUT',
            contentType: 'application/json',
            data: JSON.stringify(data)
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
            .fail(function (xhr) { alert(xhr.responseJSON && xhr.responseJSON.message || 'Gagal.'); });
    };

    self.init();
}

function TenantsViewModel() {
    var self = this;
    self.tenants = ko.observableArray([]);
    self.showForm = ko.observable(false);
    self.editingId = ko.observable(null);
    self.isAdmin = ko.observable(false);
    self.form = {
        name: ko.observable(''),
        floor: ko.observable(''),
        category: ko.observable(''),
        usesGas: ko.observable(false)
    };

    self.init = function () {
        $.getJSON('/api/tenants').done(function (d) { self.tenants(d); })
            .fail(function () { showToast('Gagal memuat daftar tenant.', 'error'); });
        $.getJSON('/api/users').done(function () {
            self.isAdmin(true);
        }).fail(function () {
            self.isAdmin(false);
        });
    };

    self.showAddForm = function () {
        self.editingId(null);
        self.form.name('');
        self.form.floor('');
        self.form.category('');
        self.form.usesGas(false);
        self.showForm(true);
    };

    self.editTenant = function (item) {
        self.editingId(item.id);
        self.form.name(item.name);
        self.form.floor(item.floor || '');
        self.form.category(item.category || '');
        self.form.usesGas(item.usesGas);
        self.showForm(true);
    };

    self.cancelForm = function () { self.showForm(false); };

    self.saveTenant = function () {
        var data = {
            name: self.form.name(),
            floor: self.form.floor(),
            category: self.form.category(),
            usesGas: self.form.usesGas()
        };
        var isEdit = !!self.editingId();
        var url = isEdit ? '/api/tenants/' + self.editingId() : '/api/tenants';
        $.ajax({ url: url, type: isEdit ? 'PUT' : 'POST', contentType: 'application/json', data: JSON.stringify(data) })
            .done(function () {
                self.showForm(false);
                self.init();
            })
            .fail(function (xhr) { showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal menyimpan.', 'error'); });
    };

    self.deleteTenant = function (item) {
        if (!confirm('Hapus tenant "' + item.name + '"?')) return;
        $.ajax({ url: '/api/tenants/' + item.id, type: 'DELETE' })
            .done(function () { self.init(); })
            .fail(function (xhr) { showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal menghapus tenant.', 'error'); });
    };

    self.init();
}

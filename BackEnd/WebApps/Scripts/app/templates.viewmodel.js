function TemplatesViewModel() {
    var self = this;
    self.templates = ko.observableArray([]);
    self.showForm = ko.observable(false);
    self.editingId = ko.observable(null);
    self.form = {
        category: ko.observable(''),
        name: ko.observable(''),
        displayOrder: ko.observable(0),
        requiresGas: ko.observable(false)
    };

    self.init = function () {
        $.getJSON('/api/templates').done(function (d) { self.templates(d); })
            .fail(function () { showToast('Gagal memuat daftar template.', 'error'); });
    };

    self.showAddForm = function () {
        self.editingId(null);
        self.form.category('');
        self.form.name('');
        self.form.displayOrder(0);
        self.form.requiresGas(false);
        self.showForm(true);
    };

    self.editTemplate = function (item) {
        self.editingId(item.id);
        self.form.category(item.category);
        self.form.name(item.name);
        self.form.displayOrder(item.displayOrder);
        self.form.requiresGas(item.requiresGas);
        self.showForm(true);
    };

    self.cancelForm = function () { self.showForm(false); };

    self.saveTemplate = function () {
        var data = {
            category: self.form.category(),
            name: self.form.name(),
            displayOrder: parseInt(self.form.displayOrder()),
            requiresGas: self.form.requiresGas()
        };
        var isEdit = !!self.editingId();
        var url = isEdit ? '/api/templates/' + self.editingId() : '/api/templates';
        $.ajax({ url: url, type: isEdit ? 'PUT' : 'POST', contentType: 'application/json', data: JSON.stringify(data) })
            .done(function () {
                self.showForm(false);
                self.init();
            })
            .fail(function (xhr) { showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal menyimpan.', 'error'); });
    };

    self.deleteTemplate = function (item) {
        if (!confirm('Hapus template "' + item.name + '"?')) return;
        $.ajax({ url: '/api/templates/' + item.id, type: 'DELETE' })
            .done(function () { self.init(); })
            .fail(function (xhr) { showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal menghapus template.', 'error'); });
    };

    self.init();
}

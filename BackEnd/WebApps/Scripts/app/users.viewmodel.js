function UsersViewModel() {
    var self = this;
    self.users = ko.observableArray([]);
    self.showForm = ko.observable(false);
    self.editingId = ko.observable(null);
    self.form = {
        username: ko.observable(''),
        name: ko.observable(''),
        password: ko.observable(''),
        role: ko.observable('Auditor')
    };

    self.init = function () {
        $.getJSON('/api/users').done(function (d) { self.users(d); })
            .fail(function () { showToast('Gagal memuat daftar pengguna.', 'error'); });
    };

    self.showAddForm = function () {
        self.editingId(null);
        self.form.username('');
        self.form.name('');
        self.form.password('');
        self.form.role('Auditor');
        self.showForm(true);
    };

    self.editUser = function (item) {
        self.editingId(item.id);
        self.form.username(item.username);
        self.form.name(item.name);
        self.form.password('');
        self.form.role(item.role);
        self.showForm(true);
    };

    self.cancelForm = function () { self.showForm(false); };

    self.saveUser = function () {
        var isEdit = !!self.editingId();
        var data = isEdit
            ? { name: self.form.name(), password: self.form.password() || undefined, role: self.form.role() }
            : { username: self.form.username(), name: self.form.name(), password: self.form.password(), role: self.form.role() };
        var url = isEdit ? '/api/users/' + self.editingId() : '/api/users';
        $.ajax({ url: url, type: isEdit ? 'PUT' : 'POST', contentType: 'application/json', data: JSON.stringify(data) })
            .done(function () {
                self.showForm(false);
                self.init();
            })
            .fail(function (xhr) { showToast(xhr.responseJSON && xhr.responseJSON.message || 'Gagal menyimpan.', 'error'); });
    };

    self.deleteUser = function (item) {
        if (!confirm('Nonaktifkan user "' + item.username + '"?')) return;
        $.ajax({ url: '/api/users/' + item.id, type: 'DELETE' })
            .done(function () { self.init(); })
            .fail(function (xhr) { showToast(xhr.responseJSON && xhr.responseJSON.message || 'Gagal menghapus pengguna.', 'error'); });
    };

    self.init();
}

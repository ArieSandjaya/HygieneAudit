function UsersViewModel() {
    var self = this;
    self.users = ko.observableArray([]);
    self.ps = new PagedSorted(self.users, 10);
    self.showForm = ko.observable(false);
    self.editingId = ko.observable(null);
    self.form = {
        username: ko.observable(''),
        name: ko.observable(''),
        password: ko.observable(''),
        role: ko.observable('Auditor')
    };

    // Reset password (admin)
    self.resetPwd = {
        userId: ko.observable(null),
        userName: ko.observable(''),
        newPassword: ko.observable(''),
        saving: ko.observable(false)
    };
    self.showResetForm = ko.observable(false);

    self.showResetPwd = function (item) {
        self.resetPwd.userId(item.id);
        self.resetPwd.userName(item.name);
        self.resetPwd.newPassword('');
        self.resetPwd.saving(false);
        self.showForm(false);
        self.showResetForm(true);
    };
    self.cancelResetPwd = function () { self.showResetForm(false); };
    self.confirmResetPwd = function () {
        var pwd = self.resetPwd.newPassword();
        if (!pwd || pwd.length < 6) { showToast('Password minimal 6 karakter.', 'error'); return; }
        self.resetPwd.saving(true);
        $.ajax({
            url: '/api/users/' + self.resetPwd.userId(),
            type: 'PUT', contentType: 'application/json',
            data: JSON.stringify({ password: pwd })
        }).done(function () {
            self.showResetForm(false);
            showToast('Password berhasil direset.');
        }).fail(function (xhr) {
            showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal mereset password.', 'error');
        }).always(function () { self.resetPwd.saving(false); });
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

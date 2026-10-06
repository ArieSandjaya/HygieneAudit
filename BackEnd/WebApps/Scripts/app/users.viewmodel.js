function UsersViewModel() {
    var self = this;

    self.users        = ko.observableArray([]);
    self.ps           = new PagedSorted(self.users, 10);
    self.showForm     = ko.observable(false);
    self.editingId    = ko.observable(null);
    self.showResetForm = ko.observable(false);

    self.form = {
        username: ko.observable(''),
        name:     ko.observable(''),
        email:    ko.observable(''),
        password: ko.observable(''),
        role:     ko.observable('Auditor'),
        notify:   ko.observable(false)
    };

    self.resetPwd = {
        userId:      ko.observable(null),
        userName:    ko.observable(''),
        newPassword: ko.observable(''),
        saving:      ko.observable(false)
    };

    self.init = function () {
        $.getJSON('/api/users').done(function (d) { self.users(d); })
            .fail(function () { showToast('Gagal memuat daftar pengguna.', 'error'); });
    };

    self.showAddForm = function () {
        self.editingId(null);
        self.form.username('');
        self.form.name('');
        self.form.email('');
        self.form.password('');
        self.form.role('Auditor');
        self.form.notify(false);
        self.showResetForm(false);
        self.showForm(true);
    };

    self.editUser = function (item) {
        self.editingId(item.id);
        self.form.username(item.username);
        self.form.name(item.name);
        self.form.email(item.email || '');
        self.form.password('');
        self.form.role(item.role);
        self.form.notify(!!item.receiveFollowUpNotification);
        self.showResetForm(false);
        self.showForm(true);
    };

    self.cancelForm = function () { self.showForm(false); };

    // Email opsional; bila diisi harus berformat valid (server memeriksa ulang + keunikan).
    var EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    self.saveUser = function () {
        var isEdit = !!self.editingId();
        var email = (self.form.email() || '').trim();
        if (email && !EMAIL_RE.test(email)) {
            showToast('Format email tidak valid.', 'error');
            return;
        }
        if (self.form.notify() && !email) {
            showToast('Isi email terlebih dahulu agar pengguna dapat menerima notifikasi.', 'error');
            return;
        }
        // Saat edit, email selalu dikirim: string kosong berarti menghapus email.
        var data = isEdit
            ? { name: self.form.name(), email: email, password: self.form.password() || undefined, role: self.form.role(), receiveFollowUpNotification: self.form.notify() }
            : { username: self.form.username(), name: self.form.name(), email: email || undefined, password: self.form.password(), role: self.form.role(), receiveFollowUpNotification: self.form.notify() };
        var url = isEdit ? '/api/users/' + self.editingId() : '/api/users';
        $.ajax({ url: url, type: isEdit ? 'PUT' : 'POST', contentType: 'application/json', data: JSON.stringify(data) })
            .done(function () { self.showForm(false); self.init(); })
            .fail(function (xhr) { showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal menyimpan.', 'error'); });
    };

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
        if (!pwd || pwd.length < 6) {
            showToast('Password minimal 6 karakter.', 'error');
            return;
        }
        self.resetPwd.saving(true);
        $.ajax({
            url: '/api/users/' + self.resetPwd.userId(),
            type: 'PUT',
            contentType: 'application/json',
            data: JSON.stringify({ password: pwd })
        }).done(function () {
            self.showResetForm(false);
            showToast('Password berhasil direset.');
        }).fail(function (xhr) {
            showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal mereset password.', 'error');
        }).always(function () {
            self.resetPwd.saving(false);
        });
    };

    self.deleteUser = function (item) {
        if (!confirm('Nonaktifkan user "' + item.username + '"?')) return;
        $.ajax({ url: '/api/users/' + item.id, type: 'DELETE' })
            .done(function () { self.init(); })
            .fail(function (xhr) { showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal menghapus pengguna.', 'error'); });
    };

    self.init();
}

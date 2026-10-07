// Pengaturan notifikasi email follow up (khusus Admin): SMTP, jam kirim, tes kirim, kirim sekarang.

function NotificationSettingsViewModel() {
    var self = this;
    var EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    var TIME_RE = /^([01]\d|2[0-3]):[0-5]\d$/;

    self.loading = ko.observable(true);
    self.saving = ko.observable(false);
    self.testing = ko.observable(false);
    self.running = ko.observable(false);

    self.f = {
        enabled:        ko.observable(false),
        smtpHost:       ko.observable(''),
        smtpPort:       ko.observable(587),
        useSsl:         ko.observable(true),
        smtpUsername:   ko.observable(''),
        smtpPassword:   ko.observable(''),
        clearPassword:  ko.observable(false),
        fromAddress:    ko.observable(''),
        fromName:       ko.observable(''),
        sendTime:       ko.observable('08:00'),
        includeOverdue: ko.observable(true),
        baseUrl:        ko.observable('')
    };
    self.hasPassword = ko.observable(false);
    self.lastRunAt = ko.observable(null);
    self.lastRunMessage = ko.observable('');
    self.recipientCount = ko.observable(0);
    self.testTo = ko.observable('');

    self.passwordHint = ko.pureComputed(function () {
        if (self.f.clearPassword()) return 'Password tersimpan akan dihapus saat disimpan.';
        return self.hasPassword() ? 'Password tersimpan. Kosongkan kolom ini untuk tetap memakainya.' : 'Belum ada password tersimpan.';
    });

    self.lastRunText = ko.pureComputed(function () {
        var v = self.lastRunAt();
        if (!v) return 'Belum pernah dikirim.';
        var d = new Date(v);
        return d.toLocaleString('id-ID', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    });

    function apply(d) {
        self.f.enabled(!!d.enabled);
        self.f.smtpHost(d.smtpHost || '');
        self.f.smtpPort(d.smtpPort || 587);
        self.f.useSsl(d.useSsl !== false);
        self.f.smtpUsername(d.smtpUsername || '');
        self.f.smtpPassword('');
        self.f.clearPassword(false);
        self.f.fromAddress(d.fromAddress || '');
        self.f.fromName(d.fromName || '');
        self.f.sendTime(d.sendTime || '08:00');
        self.f.includeOverdue(d.includeOverdue !== false);
        self.f.baseUrl(d.baseUrl || '');
        self.hasPassword(!!d.hasPassword);
        self.lastRunAt(d.lastRunAt || null);
        self.lastRunMessage(d.lastRunMessage || '');
        self.recipientCount(d.recipientCount || 0);
    }

    function errMsg(xhr, fallback) {
        return (xhr && xhr.responseJSON && xhr.responseJSON.message) || fallback;
    }

    self.load = function () {
        self.loading(true);
        $.getJSON('/api/settings/notifications').done(function (d) {
            apply(d);
            if (!self.testTo()) self.testTo(d.fromAddress || '');
        }).fail(function (xhr) {
            showToast(errMsg(xhr, 'Gagal memuat pengaturan.'), 'error');
        }).always(function () { self.loading(false); });
    };

    function validate() {
        var host = (self.f.smtpHost() || '').trim();
        var from = (self.f.fromAddress() || '').trim();
        var port = parseInt(self.f.smtpPort(), 10);
        if (!TIME_RE.test((self.f.sendTime() || '').trim())) { showToast('Jam kirim harus berformat HH:mm.', 'error'); return false; }
        if (!(port >= 1 && port <= 65535)) { showToast('Port SMTP harus antara 1 dan 65535.', 'error'); return false; }
        if (from && !EMAIL_RE.test(from)) { showToast('Alamat pengirim tidak valid.', 'error'); return false; }
        if (self.f.enabled() && (!host || !from)) { showToast('Host SMTP dan alamat pengirim wajib diisi untuk mengaktifkan notifikasi.', 'error'); return false; }
        return true;
    }

    self.save = function () {
        if (self.saving() || !validate()) return;
        var data = {
            enabled: self.f.enabled(),
            smtpHost: (self.f.smtpHost() || '').trim(),
            smtpPort: parseInt(self.f.smtpPort(), 10),
            useSsl: self.f.useSsl(),
            smtpUsername: (self.f.smtpUsername() || '').trim(),
            smtpPassword: self.f.smtpPassword() || null,
            clearPassword: self.f.clearPassword(),
            fromAddress: (self.f.fromAddress() || '').trim(),
            fromName: (self.f.fromName() || '').trim(),
            sendTime: (self.f.sendTime() || '').trim(),
            includeOverdue: self.f.includeOverdue(),
            baseUrl: (self.f.baseUrl() || '').trim()
        };
        self.saving(true);
        $.ajax({ url: '/api/settings/notifications', type: 'PUT', contentType: 'application/json', data: JSON.stringify(data) })
            .done(function (d) { apply(d); showToast('Pengaturan tersimpan.'); })
            .fail(function (xhr) { showToast(errMsg(xhr, 'Gagal menyimpan pengaturan.'), 'error'); })
            .always(function () { self.saving(false); });
    };

    self.sendTest = function () {
        var to = (self.testTo() || '').trim();
        if (!EMAIL_RE.test(to)) { showToast('Isi alamat email tujuan yang valid.', 'error'); return; }
        if (self.testing()) return;
        self.testing(true);
        $.ajax({ url: '/api/settings/notifications/test', type: 'POST', contentType: 'application/json', data: JSON.stringify({ to: to }) })
            .done(function (r) { showToast((r && r.message) || 'Email uji terkirim.'); })
            .fail(function (xhr) { showToast(errMsg(xhr, 'Gagal mengirim email uji.'), 'error'); })
            .always(function () { self.testing(false); });
    };

    self.runNow = function () {
        if (self.running()) return;
        if (!confirm('Kirim pengingat sekarang ke semua penerima untuk temuan yang sudah jatuh tempo?')) return;
        self.running(true);
        $.ajax({ url: '/api/settings/notifications/run', type: 'POST' })
            .done(function (r) {
                showToast((r && r.message) || 'Selesai.', r && (r.failed > 0 || !r.sent) ? 'error' : undefined);
                self.load();   // pesan lengkap tampil di kartu Status
            })
            .fail(function (xhr) { showToast(errMsg(xhr, 'Gagal menjalankan pengiriman.'), 'error'); })
            .always(function () { self.running(false); });
    };

    self.load();
}

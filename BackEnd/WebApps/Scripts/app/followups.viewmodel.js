// Menu Follow Up: tindak lanjut item audit yang FAIL.

function fuFormatDate(d) {
    return d ? new Date(d).toLocaleDateString('id-ID') : '-';
}

// yyyy-mm-dd (zona waktu lokal) untuk <input type="date">
function fuIsoDate(d) {
    var dt = d ? new Date(d) : new Date();
    var m = dt.getMonth() + 1, day = dt.getDate();
    return dt.getFullYear() + '-' + (m < 10 ? '0' + m : m) + '-' + (day < 10 ? '0' + day : day);
}

// Kompres gambar menjadi JPEG data URL (sisi terpanjang <= maxSide). Fallback: baca apa adanya.
function fuCompressToDataUrl(file, maxSide, quality) {
    return new Promise(function (resolve, reject) {
        var objectUrl = URL.createObjectURL(file);
        var img = new Image();
        img.onload = function () {
            var w = img.naturalWidth || img.width;
            var h = img.naturalHeight || img.height;
            if (w > maxSide || h > maxSide) {
                var ratio = Math.min(maxSide / w, maxSide / h);
                w = Math.round(w * ratio);
                h = Math.round(h * ratio);
            }
            var canvas = document.createElement('canvas');
            canvas.width = w;
            canvas.height = h;
            canvas.getContext('2d').drawImage(img, 0, 0, w, h);
            URL.revokeObjectURL(objectUrl);
            resolve(canvas.toDataURL('image/jpeg', quality));
        };
        img.onerror = function () {
            URL.revokeObjectURL(objectUrl);
            reject(new Error('Gambar tidak dapat dibaca.'));
        };
        img.src = objectUrl;
    });
}

// ---------------------------------------------------------------- Daftar Follow Up
function FollowUpsViewModel() {
    var self = this;
    self.audits    = ko.observableArray([]);
    self.loading   = ko.observable(true);
    self.activeTab = ko.observable('pending');   // pending = belum 100%, done = sudah 100% via follow up
    self.search    = ko.observable('');

    self.hasSearch   = ko.computed(function () { return self.search().trim().length > 0; });
    self.clearSearch = function () { self.search(''); };
    self.setTab      = function (tab) { self.activeTab(tab); };

    self.searched = ko.computed(function () {
        var q = self.search().trim().toLowerCase();
        if (!q) return self.audits();
        return self.audits().filter(function (a) {
            return (a.tenantName || '').toLowerCase().indexOf(q) >= 0;
        });
    });
    self.pendingCount = ko.computed(function () {
        return self.searched().filter(function (a) { return a.failCount > 0; }).length;
    });
    self.doneCount = ko.computed(function () {
        return self.searched().filter(function (a) { return a.failCount === 0; }).length;
    });
    self.filtered = ko.computed(function () {
        var pending = self.activeTab() === 'pending';
        return self.searched().filter(function (a) { return pending ? a.failCount > 0 : a.failCount === 0; });
    });

    self.dateLabel     = function (a) { return fuFormatDate(a.date); };
    self.rateLabel     = function (a) { return Math.round(a.passRate) + '%'; };
    self.rateCss       = function (a) { return a.failCount === 0 ? 'bg-label-success' : 'bg-label-danger'; };
    self.failLabel     = function (a) { return a.failCount + ' item belum lulus'; };
    self.followUpLabel = function (a) {
        return a.followUpCount > 0
            ? a.followUpCount + 'x follow up, terakhir ' + fuFormatDate(a.lastFollowUpAt)
            : 'Belum pernah di-follow up';
    };
    self.detailUrl     = function (a) { return '/FollowUps/Detail/' + a.id; };
    self.hasTarget     = function (a) { return a.failCount > 0 && !!a.nextTargetDate; };
    self.targetLabel   = function (a) {
        return 'Target follow up: ' + fuFormatDate(a.nextTargetDate) + (a.overdueCount > 0 ? ' (' + a.overdueCount + ' terlambat)' : '');
    };

    self.init = function () {
        $.getJSON('/api/followups').done(function (data) {
            self.audits(data);
        }).fail(function () {
            showToast('Gagal memuat daftar follow up.', 'error');
        }).always(function () { self.loading(false); });
    };
    self.init();
}

// ---------------------------------------------------------------- Detail Follow Up
function FollowUpDetailViewModel(auditId) {
    var self = this;
    var MAX_PHOTOS = 4;

    self.detail  = ko.observable(null);
    self.loading = ko.observable(true);
    self.items   = ko.observableArray([]);

    self.tenantName = ko.computed(function () { return self.detail() ? self.detail().tenantName : ''; });
    self.picName    = ko.computed(function () { return self.detail() ? self.detail().picName : ''; });
    self.auditDate  = ko.computed(function () { return self.detail() ? fuFormatDate(self.detail().date) : ''; });
    self.isGas      = ko.computed(function () { return self.detail() ? self.detail().isGas : false; });
    self.passCount  = ko.computed(function () { return self.detail() ? self.detail().passCount : 0; });
    self.failCount  = ko.computed(function () { return self.detail() ? self.detail().failCount : 0; });
    self.totalCount = ko.computed(function () { return self.detail() ? self.detail().totalItems : 0; });
    self.passRate   = ko.computed(function () { return self.detail() ? Math.round(self.detail().passRate) : 0; });

    self.progressWidth = ko.computed(function () { return self.passRate() + '%'; });
    self.progressColor = ko.computed(function () { return self.failCount() === 0 ? '#22c55e' : '#ef4444'; });

    self.openItems = ko.computed(function () {
        return self.items().filter(function (i) { return i.status === 'FAIL'; });
    });
    self.resolvedItems = ko.computed(function () {
        return self.items().filter(function (i) { return i.status === 'PASS'; });
    });

    self.dateText      = function (d) { return fuFormatDate(d); };
    self.targetText   = function (item) {
        return item.targetDate ? 'Target follow up: ' + fuFormatDate(item.targetDate) + (item.isOverdue ? ' (terlambat)' : '') : 'Target follow up: belum ditentukan';
    };
    self.historyMeta   = function (h) { return fuFormatDate(h.date) + ' \u2022 ' + h.picName; };
    self.resultCss     = function (h) { return h.result === 'PASS' ? 'ha-badge--green' : 'bg-label-danger'; };
    self.resultText    = function (h) { return h.result === 'PASS' ? 'PASS' : 'FAIL'; };
    self.historyTitle  = function (item) { return 'Riwayat Follow Up (' + item.followUps.length + ')'; };
    self.minDate       = ko.computed(function () { return self.detail() ? fuIsoDate(self.detail().date) : ''; });
    self.maxDate       = fuIsoDate();

    function decorate(item) {
        item.formOpen    = ko.observable(false);
        item.fDate       = ko.observable(fuIsoDate());
        item.fResult     = ko.observable('');
        item.fNote       = ko.observable('');
        item.fPhotos     = ko.observableArray([]);
        item.saving      = ko.observable(false);
        item.showHistory = ko.observable(false);
        item.noteInvalid = ko.computed(function () { return item.formOpen() && !item.fNote().trim(); });
        item.hasHistory  = item.followUps.length > 0;
        item.hasPhotos   = item.photos.length > 0;
        item.hasNote     = !!(item.note && item.note.trim());
        item.canSubmit   = ko.computed(function () {
            return !item.saving() && item.fResult() !== '' && item.fNote().trim().length > 0;
        });
        return item;
    }

    self.init = function () {
        self.loading(true);
        $.getJSON('/api/followups/' + auditId).done(function (d) {
            self.detail(d);
            self.items((d.items || []).map(decorate));
        }).fail(function () {
            showToast('Gagal memuat data follow up. Silakan kembali dan coba lagi.', 'error');
        }).always(function () { self.loading(false); });
    };

    self.toggleForm    = function (item) { item.formOpen(!item.formOpen()); };
    self.toggleHistory = function (item) { item.showHistory(!item.showHistory()); };
    self.setResult     = function (item, result) { item.fResult(item.fResult() === result ? '' : result); };

    self.addPhoto = function (item, event) {
        var fileList = event.target.files;
        if (!fileList || !fileList.length) return;
        var files = Array.prototype.slice.call(fileList);
        event.target.value = '';   // reset agar file yang sama bisa dipilih lagi

        files.forEach(function (f) {
            if (item.fPhotos().length >= MAX_PHOTOS) {
                showToast('Maksimal ' + MAX_PHOTOS + ' foto per follow up.', 'error');
                return;
            }
            fuCompressToDataUrl(f, 1280, 0.82).then(function (dataUrl) {
                if (item.fPhotos().length < MAX_PHOTOS) item.fPhotos.push(dataUrl);
            }).catch(function () {
                showToast('Gagal membaca foto.', 'error');
            });
        });
    };
    self.removePhoto = function (item, dataUrl) { item.fPhotos.remove(dataUrl); };

    self.submit = function (item) {
        if (item.saving()) return;
        if (!item.fResult()) { showToast('Pilih hasil follow up (Pass / Fail).', 'error'); return; }
        if (!item.fNote().trim()) { showToast('Catatan follow up wajib diisi.', 'error'); return; }

        var isPass = item.fResult() === 'PASS';
        item.saving(true);
        $.ajax({
            url: '/api/followups/' + auditId + '/items/' + item.id,
            type: 'POST', contentType: 'application/json',
            data: JSON.stringify({
                date: item.fDate(),
                result: item.fResult(),
                note: item.fNote().trim(),
                photos: item.fPhotos()
            })
        }).done(function () {
            showToast(isPass
                ? 'Follow up tersimpan. Item berubah menjadi Pass dan nilai audit diperbarui.'
                : 'Follow up tersimpan. Item masih Fail.');
            self.init();   // muat ulang: skor, daftar item, dan riwayat
        }).fail(function (xhr) {
            item.saving(false);
            showToast((xhr.responseJSON && xhr.responseJSON.message) || 'Gagal menyimpan follow up.', 'error');
        });
    };

    self.init();
}

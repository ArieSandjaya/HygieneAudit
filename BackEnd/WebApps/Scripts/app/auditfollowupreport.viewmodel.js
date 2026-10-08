// Laporan Audit & Follow Up: hasil audit (nilai awal) dihubungkan langsung dengan hasil follow up (nilai kini).

function AuditFollowUpReportViewModel() {
    var self = this;
    var seq = 0;          // hanya respons permintaan terakhir yang dipakai
    var timer = null;     // debounce pencarian

    self.rows    = ko.observableArray([]);
    self.ps      = new PagedSorted(self.rows, 10);
    self.summary = ko.observable(null);
    self.loading = ko.observable(true);
    self.openId  = ko.observable(null);   // audit yang rinciannya sedang dibuka
    self.filter  = {
        status: ko.observable('all'),
        type:   ko.observable('all'),
        search: ko.observable(''),
        from:   ko.observable(''),
        to:     ko.observable('')
    };

    function queryString() {
        return 'status=' + encodeURIComponent(self.filter.status()) +
            '&type=' + encodeURIComponent(self.filter.type()) +
            '&search=' + encodeURIComponent(self.filter.search().trim()) +
            '&dateFrom=' + encodeURIComponent(self.filter.from()) +
            '&dateTo=' + encodeURIComponent(self.filter.to());
    }

    self.exportUrl = ko.computed(function () {
        return '/api/reports/audit-followups/export-excel?' + queryString();
    });

    self.rangeInvalid = ko.computed(function () {
        return !!self.filter.from() && !!self.filter.to() && self.filter.from() > self.filter.to();
    });

    self.loadReport = function () {
        if (self.rangeInvalid()) {
            showToast('Tanggal "Dari" tidak boleh setelah "Sampai".', 'error');
            return;
        }
        var mine = ++seq;
        self.loading(true);
        $.getJSON('/api/reports/audit-followups?' + queryString()).done(function (data) {
            if (mine !== seq) return;
            self.openId(null);
            self.rows(data.rows || []);
            self.summary(data.summary || null);
        }).fail(function () {
            if (mine !== seq) return;
            showToast('Gagal memuat laporan audit & follow up.', 'error');
        }).always(function () {
            if (mine === seq) self.loading(false);
        });
    };

    self.searchChanged = function () {
        clearTimeout(timer);
        timer = setTimeout(self.loadReport, 300);
    };

    self.clearFilters = function () {
        self.filter.status('all');
        self.filter.type('all');
        self.filter.search('');
        self.filter.from('');
        self.filter.to('');
        self.loadReport();
    };

    // ---- rincian per audit
    self.isOpen = function (r) { return self.openId() === r.auditId; };
    self.toggle = function (r) { self.openId(self.isOpen(r) ? null : r.auditId); };
    self.hasItems = function (r) { return !!(r.items && r.items.length); };

    // ---- label tampilan
    function fmt(d) { return d ? new Date(d).toLocaleDateString('id-ID') : '-'; }
    function pct(v) { return (Math.round(v * 10) / 10) + '%'; }

    self.auditDateText = function (r) { return fmt(r.auditDate); };
    self.detailUrl     = function (r) { return '/FollowUps/Detail/' + r.auditId; };
    self.dateText      = function (d) { return fmt(d); };
    self.targetText    = function (f) { return f.targetDate ? fmt(f.targetDate) : '-'; };

    // Hijau hanya bila 100%.
    self.rateCss = function (v, total) { return total > 0 && v >= 100 ? 'text-success' : 'text-danger'; };
    self.initialText = function (r) { return r.totalItems > 0 ? pct(r.initialRate) : '-'; };
    self.currentText = function (r) { return r.totalItems > 0 ? pct(r.currentRate) : '-'; };
    self.deltaText = function (r) {
        if (!(r.totalItems > 0) || r.currentRate === r.initialRate) return '';
        var d = Math.round((r.currentRate - r.initialRate) * 10) / 10;
        return (d > 0 ? '+' : '') + d + ' poin';
    };

    self.statusText = function (r) {
        if (r.status === 'CLEAN')    return 'Lulus';
        if (r.status === 'RESOLVED') return 'Temuan selesai';
        if (r.status === 'OVERDUE')  return 'Terlambat';
        return 'Berjalan';
    };
    self.statusCss = function (r) {
        if (r.status === 'CLEAN' || r.status === 'RESOLVED') return 'bg-label-success';
        if (r.status === 'OVERDUE') return 'bg-label-danger';
        return 'bg-label-warning';
    };
    self.findingsText = function (r) {
        if (!r.findings) return 'Tidak ada temuan';
        return r.resolved + '/' + r.findings + ' selesai' +
            (r.overdue ? ' • ' + r.overdue + ' terlambat' : '');
    };

    self.findingStatusText = function (f) {
        if (f.status === 'RESOLVED') return f.resolvedLate ? 'Selesai (lewat target)' : 'Selesai';
        if (f.status === 'OVERDUE')  return 'Terlambat ' + f.daysOverdue + ' hari';
        return 'Terbuka';
    };
    self.findingStatusCss = function (f) {
        if (f.status === 'RESOLVED') return f.resolvedLate ? 'bg-label-warning' : 'bg-label-success';
        if (f.status === 'OVERDUE')  return 'bg-label-danger';
        return 'bg-label-secondary';
    };
    self.resultCss = function (e) { return e.result === 'PASS' ? 'bg-label-success' : 'bg-label-danger'; };

    self.avgInitialText = ko.computed(function () { var s = self.summary(); return s && s.audits ? pct(s.averageInitialRate) : '-'; });
    self.avgCurrentText = ko.computed(function () { var s = self.summary(); return s && s.audits ? pct(s.averageCurrentRate) : '-'; });

    self.loadReport();
}

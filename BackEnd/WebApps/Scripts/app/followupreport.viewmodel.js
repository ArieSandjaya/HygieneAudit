// Laporan Follow Up: satu baris per temuan, dengan ringkasan, filter, dan export Excel.

function FollowUpReportViewModel() {
    var self = this;
    var seq = 0;          // hanya respons permintaan terakhir yang dipakai
    var timer = null;     // debounce pencarian

    self.rows    = ko.observableArray([]);
    self.ps      = new PagedSorted(self.rows, 10);
    self.summary = ko.observable(null);
    self.loading = ko.observable(true);
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
        return '/api/reports/followups/export-excel?' + queryString();
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
        $.getJSON('/api/reports/followups?' + queryString()).done(function (data) {
            if (mine !== seq) return;
            self.rows(data.rows || []);
            self.summary(data.summary || null);
        }).fail(function () {
            if (mine !== seq) return;
            showToast('Gagal memuat laporan follow up.', 'error');
        }).always(function () {
            if (mine === seq) self.loading(false);
        });
    };

    // Dipakai kotak pencarian: tunggu pengguna selesai mengetik.
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

    // ---- label tampilan
    function fmt(d) { return d ? new Date(d).toLocaleDateString('id-ID') : '-'; }

    self.auditDateText = function (r) { return fmt(r.auditDate); };
    self.targetText    = function (r) { return r.targetDate ? fmt(r.targetDate) : '-'; };
    self.detailUrl     = function (r) { return '/FollowUps/Detail/' + r.auditId; };

    self.statusText = function (r) {
        if (r.status === 'RESOLVED') return r.resolvedLate ? 'Selesai (lewat target)' : 'Selesai';
        if (r.status === 'OVERDUE')  return 'Terlambat ' + r.daysOverdue + ' hari';
        return 'Terbuka';
    };
    self.statusCss = function (r) {
        if (r.status === 'RESOLVED') return r.resolvedLate ? 'bg-label-warning' : 'bg-label-success';
        if (r.status === 'OVERDUE')  return 'bg-label-danger';
        return 'bg-label-secondary';
    };

    self.hasFollowUp   = function (r) { return r.followUpCount > 0; };
    self.followUpText  = function (r) {
        return r.followUpCount + 'x \u2022 ' + fmt(r.lastFollowUpDate) + (r.lastFollowUpBy ? ' \u2022 ' + r.lastFollowUpBy : '');
    };
    self.resultCss     = function (r) { return r.lastFollowUpResult === 'PASS' ? 'bg-label-success' : 'bg-label-danger'; };
    self.resolvedText  = function (r) {
        if (!r.resolvedDate) return '-';
        return fmt(r.resolvedDate) + (r.daysToResolve != null ? ' (' + r.daysToResolve + ' hari)' : '');
    };
    self.rateText      = ko.computed(function () {
        var s = self.summary();
        return s ? s.resolutionRate + '%' : '-';
    });
    self.avgDaysText   = ko.computed(function () {
        var s = self.summary();
        return s && s.resolved > 0 ? s.averageDaysToResolve + ' hari' : '-';
    });

    self.loadReport();
}

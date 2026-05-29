function ReportsViewModel() {
    var self = this;
    self.rows = ko.observableArray([]);
    self.summary = ko.observable(null);
    self.filter = {
        status: ko.observable('all'),
        type: ko.observable('all'),
        search: ko.observable('')
    };

    self.exportUrl = ko.computed(function () {
        return '/api/reports/export-excel?status=' + self.filter.status() +
            '&type=' + self.filter.type() +
            '&search=' + encodeURIComponent(self.filter.search());
    });

    self.loadReport = function () {
        var url = '/api/reports/latest-per-tenant?status=' + self.filter.status() +
            '&type=' + self.filter.type() +
            '&search=' + encodeURIComponent(self.filter.search());
        $.getJSON(url).done(function (data) {
            self.rows(data.rows || []);
            self.summary(data.summary || null);
        }).fail(function () {
            showToast('Gagal memuat laporan.', 'error');
        });
    };

    self.loadReport();
}

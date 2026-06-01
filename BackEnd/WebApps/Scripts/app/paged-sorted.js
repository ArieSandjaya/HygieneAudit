function PagedSorted(sourceObs, pageSize) {
    var self = this;
    self.pageSize = pageSize || 10;
    self.currentPage = ko.observable(1);
    self.sortField = ko.observable('');
    self.sortAsc = ko.observable(true);

    self.sortBy = function (field) {
        if (self.sortField() === field) {
            self.sortAsc(!self.sortAsc());
        } else {
            self.sortField(field);
            self.sortAsc(true);
        }
        self.currentPage(1);
    };

    self.sortedItems = ko.computed(function () {
        var items = sourceObs().slice();
        var field = self.sortField();
        if (!field) return items;
        var asc = self.sortAsc();
        items.sort(function (a, b) {
            var av = a[field], bv = b[field];
            if (av == null) av = '';
            if (bv == null) bv = '';
            if (typeof av === 'string') av = av.toLowerCase();
            if (typeof bv === 'string') bv = bv.toLowerCase();
            return av < bv ? (asc ? -1 : 1) : av > bv ? (asc ? 1 : -1) : 0;
        });
        return items;
    });

    self.totalPages = ko.computed(function () {
        return Math.max(1, Math.ceil(self.sortedItems().length / self.pageSize));
    });

    self.pagedItems = ko.computed(function () {
        var start = (self.currentPage() - 1) * self.pageSize;
        return self.sortedItems().slice(start, start + self.pageSize);
    });

    self.pageNumbers = ko.computed(function () {
        var total = self.totalPages();
        var current = self.currentPage();
        var pages = [];
        for (var i = 1; i <= total; i++) {
            if (i === 1 || i === total || Math.abs(i - current) <= 2) {
                pages.push(i);
            }
        }
        return pages;
    });

    self.goTo = function (page) {
        self.currentPage(Math.max(1, Math.min(self.totalPages(), +page)));
    };
    self.prev = function () { if (self.currentPage() > 1) self.currentPage(self.currentPage() - 1); };
    self.next = function () { if (self.currentPage() < self.totalPages()) self.currentPage(self.currentPage() + 1); };
    self.hasPrev = ko.computed(function () { return self.currentPage() > 1; });
    self.hasNext = ko.computed(function () { return self.currentPage() < self.totalPages(); });

    self.infoText = ko.computed(function () {
        var total = self.sortedItems().length;
        if (!total) return '';
        var start = (self.currentPage() - 1) * self.pageSize + 1;
        var end = Math.min(self.currentPage() * self.pageSize, total);
        return start + '–' + end + ' dari ' + total;
    });

    sourceObs.subscribe(function () { self.currentPage(1); });
}

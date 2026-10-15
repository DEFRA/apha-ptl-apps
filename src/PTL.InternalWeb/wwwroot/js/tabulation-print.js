// Port of legacy TabulationTables.js. Rebuilds each tabulation table for printing, moving columns
// into a new table once the table is wider than the printable page, then stretches wide tables and
// inserts page breaks. The data is the server-rendered JSON in #tabulation-print-data.
(function () {
    "use strict";

    var pageHeight = 720;
    var pageWidth = 650;
    var stretchWidth = 300;

    function cell(text, className) {
        var td = document.createElement("td");
        td.textContent = text;
        td.className = className;
        return td;
    }

    function withSpace(text) {
        return (text || "") + "\u00a0";
    }

    function CustomTable(testName, participants) {
        this.table = document.createElement("table");
        this.table.className = "TabulationTable";
        var body = document.createElement("tbody");

        var header = document.createElement("tr");
        var headerCell = cell(testName, "TabulationHeader1");
        headerCell.rowSpan = 2;
        header.appendChild(headerCell);
        body.appendChild(header);
        body.appendChild(document.createElement("tr"));

        var intended = document.createElement("tr");
        var intendedCell = cell("Intended", "TabulationCell");
        intendedCell.rowSpan = 2;
        intended.appendChild(intendedCell);
        body.appendChild(intended);
        body.appendChild(document.createElement("tr"));

        participants.forEach(function (p) {
            var row = document.createElement("tr");
            row.appendChild(cell(withSpace(p.labCode), "TabulationCell"));
            body.appendChild(row);
        });

        this.table.appendChild(body);
        this.participants = participants;
    }

    // A participant without results gets one Nil Return cell that grows across the columns.
    CustomTable.prototype.addNilReturn = function (rowIndex, isFirstColumn) {
        var row = this.table.rows[4 + rowIndex];
        if (isFirstColumn) {
            row.appendChild(cell(this.participants[rowIndex].nilReturnReason, "TabulationCell NilReturn"));
        } else {
            row.cells[row.cells.length - 1].colSpan += 1;
        }
    };

    CustomTable.prototype.addMethodColumn = function (column) {
        var header = cell(column.headerText, "TabulationHeader2");
        header.rowSpan = 2;
        this.table.rows[0].appendChild(header);
        this.table.rows[2].appendChild(cell("\u00a0", "TabulationCell"));
        this.table.rows[3].appendChild(cell(withSpace(column.intendedResult), "TabulationCell"));

        var isFirstColumn = this.table.rows[0].cells.length === 2;
        for (var i = 0; i < column.results.length; i++) {
            if (this.participants[i].hasResults) {
                this.table.rows[4 + i].appendChild(cell(column.results[i], "TabulationCell"));
            } else {
                this.addNilReturn(i, isFirstColumn);
            }
        }
    };

    CustomTable.prototype.addSampleColumn = function (sample) {
        var header = cell(sample.sampleNumber, "TabulationHeader1");
        header.colSpan = sample.resultColumns.length;
        this.table.rows[0].appendChild(header);

        var intended = cell(withSpace(sample.tabulationDescription), "TabulationCell");
        intended.colSpan = sample.resultColumns.length;
        this.table.rows[2].appendChild(intended);

        for (var i = 0; i < sample.resultColumns.length; i++) {
            var column = sample.resultColumns[i];
            this.table.rows[1].appendChild(cell(column.headerText, "TabulationHeader2"));
            this.table.rows[3].appendChild(cell(withSpace(column.intendedResult), "TabulationCell"));

            for (var j = 0; j < column.results.length; j++) {
                if (this.participants[j].hasResults) {
                    var result = column.results[j];
                    if (result.span > 0) {
                        var td = cell(result.result, "TabulationCell");
                        td.colSpan = result.span;
                        this.table.rows[4 + j].appendChild(td);
                    }
                } else {
                    this.addNilReturn(j, this.table.rows[4 + j].cells.length === 1);
                }
            }
        }
    };

    function RatingTable(participants) {
        this.table = document.createElement("table");
        this.table.className = "TabulationTable";
        var body = document.createElement("tbody");

        var header = document.createElement("tr");
        header.appendChild(cell("QAU Ratings", "TabulationHeader1"));
        body.appendChild(header);

        participants.forEach(function (p) {
            var row = document.createElement("tr");
            row.appendChild(cell(withSpace(p.labCode), "TabulationCell"));
            body.appendChild(row);
        });

        ["Mean score of all laboratories: ",
            "No. of Labs reporting intended results: ",
            "No. Of Participants: ",
            "Percent Correct: "].forEach(function (label) {
            var row = document.createElement("tr");
            row.appendChild(cell(label, "TabulationCell"));
            body.appendChild(row);
        });

        this.table.appendChild(body);
    }

    RatingTable.prototype.addRatingColumn = function (column) {
        var rows = this.table.rows;
        rows[0].appendChild(cell(column.sampleNumber, "TabulationHeader2"));
        for (var i = 0; i < column.ratings.length; i++) {
            rows[1 + i].appendChild(cell(column.ratings[i], "TabulationCell"));
        }

        var offset = column.ratings.length;
        rows[1 + offset].appendChild(cell(column.sampleMean, "TabulationHeader2"));
        rows[2 + offset].appendChild(cell(column.numberReportingIntendedResult, "TabulationHeader2"));
        rows[3 + offset].appendChild(cell(column.numberOfParticipants, "TabulationHeader2"));
        rows[4 + offset].appendChild(cell(column.samplePassRate, "TabulationHeader2"));
    };

    // Adds columns one at a time; when a column pushes the table past the page width (and it is not
    // the table's first column) that column is removed and drawn again at the start of a new table.
    function addColumns(container, columns, createTable, addColumn) {
        var table = createTable();
        container.appendChild(table.table);
        container.appendChild(document.createElement("br"));

        for (var i = 0; i < columns.length; i++) {
            var previous = table.table.cloneNode(true);
            var isFirstColumn = table.table.rows[0].cells.length === 1;
            addColumn(table, columns[i]);

            if (table.table.offsetWidth > pageWidth && !isFirstColumn) {
                table.table.parentNode.replaceChild(previous, table.table);
                table = createTable();
                container.appendChild(table.table);
                container.appendChild(document.createElement("br"));
                i--;
            }
        }
    }

    function build(container, data) {
        container.style.width = pageWidth + "px";
        var participants = data.participants;

        data.tests.forEach(function (test) {
            var columns = test.methodColumns.map(function (c) { return { kind: "method", value: c }; })
                .concat(test.sampleColumns
                    .filter(function (s) { return s.resultColumns.length > 0; })
                    .map(function (s) { return { kind: "sample", value: s }; }));

            addColumns(container, columns,
                function () { return new CustomTable(test.name, participants); },
                function (table, column) {
                    if (column.kind === "method") {
                        table.addMethodColumn(column.value);
                    } else {
                        table.addSampleColumn(column.value);
                    }
                });
        });

        if (data.ratingColumns.length > 0) {
            addColumns(container, data.ratingColumns,
                function () { return new RatingTable(participants); },
                function (table, column) { table.addRatingColumn(column); });
        }

        var tables = container.getElementsByTagName("table");
        for (var t = 0; t < tables.length; t++) {
            if (tables[t].offsetWidth >= stretchWidth) {
                tables[t].style.width = pageWidth + "px";
            }
        }

        var totalHeight = 0;
        for (var k = 1; k < tables.length; k++) {
            if (-totalHeight + tables[k].offsetTop + tables[k].offsetHeight > pageHeight && !tables[k].classList.contains("nobreak")) {
                totalHeight = tables[k].offsetTop;
                tables[k - 1].style.pageBreakAfter = "always";
            }
        }

        container.classList.add("printonly");
    }

    document.addEventListener("DOMContentLoaded", function () {
        var dataElement = document.getElementById("tabulation-print-data");
        var container = document.getElementById("tabulation-print-tables");
        if (!dataElement || !container) {
            return;
        }

        build(container, JSON.parse(dataElement.textContent));
    });
})();

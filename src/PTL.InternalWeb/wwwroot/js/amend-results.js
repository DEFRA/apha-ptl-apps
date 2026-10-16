// Amend Results (legacy Scheduling/AmendResults.aspx scripts): Range toggles the second intended
// value, Nil Return reasons stay in step across test tables, and the QAU Ratings statistics are a
// port of legacy fnStatsVisible() / CalculateStats().
(function () {
    "use strict";

    var form = document.querySelector("form[data-amend-results]");
    if (!form) {
        return;
    }

    var NOT_SCORED = "Not Scored";

    function each(selector, fn) {
        Array.prototype.forEach.call(form.querySelectorAll(selector), fn);
    }

    // ---- Range ---------------------------------------------------------------------------------

    each("input[data-amend-range]", function (box) {
        box.addEventListener("change", function () {
            each("[data-amend-range-second]", function (span) {
                if (span.dataset.amendRangeSecond === box.name) {
                    span.hidden = !box.checked;
                }
            });
        });
    });

    // ---- Nil Return ----------------------------------------------------------------------------

    form.addEventListener("input", function (event) {
        var source = event.target;
        if (!source.matches?.("input[data-amend-nil-return]")) {
            return;
        }
        each("input[data-amend-nil-return]", function (other) {
            if (other !== source && other.name === source.name) {
                other.value = source.value;
            }
        });
    });

    // ---- QAU Ratings statistics ----------------------------------------------------------------

    var status = form.querySelector("[data-amend-stats-status]");
    var recalculate = form.querySelector("[data-amend-recalculate]");
    if (!recalculate) {
        return;
    }

    function setStat(stat, sampleId, text) {
        each(`[data-stat='${stat}']`, function (cell) {
            if (cell.dataset.sampleId === sampleId) {
                cell.textContent = text;
            }
        });
    }

    function sampleIds() {
        var ids = [];
        each("[data-stat='mean']", function (cell) {
            ids.push(cell.dataset.sampleId);
        });
        return ids;
    }

    function statsVisible() {
        each("[data-stat]", function (cell) {
            cell.textContent = "Recalculate";
        });
        if (status) {
            status.textContent = "The statistics need to be recalculated.";
        }
    }

    function calculateStats() {
        sampleIds().forEach(function (sampleId) {
            var participantCount = 0;
            var labsReportingCount = 0;
            var isNotScored = false;
            var sum = 0;
            var meanScore;
            var percentageCorrect;

            each("select[data-amend-rating]", function (select) {
                if (select.dataset.sampleId !== sampleId || select.selectedIndex < 0) {
                    return;
                }
                var rating = select.options[select.selectedIndex].text;
                // Legacy compared the rating text loosely with numbers ("2" == 2).
                var value = Number(rating);

                if (value === 2) {
                    labsReportingCount++;
                }
                if (value === -1 || value === 0 || value === 1 || value === 2) {
                    participantCount++;
                    sum = sum + value;
                }
                if (rating === NOT_SCORED) {
                    isNotScored = true;
                    participantCount++;
                }
            });

            var labsReporting = String(labsReportingCount);
            if (isNotScored) {
                meanScore = NOT_SCORED;
                percentageCorrect = NOT_SCORED;
                labsReporting = NOT_SCORED;
            } else {
                if (participantCount !== 0) {
                    meanScore = (sum / participantCount).toFixed(1);
                    percentageCorrect = `${((labsReportingCount / participantCount) * 100).toFixed(1)}%`;
                } else {
                    meanScore = (0).toFixed(1);
                    percentageCorrect = `${(0).toFixed(1)}%`;
                }
                if (labsReportingCount === 0) {
                    percentageCorrect = `${(0).toFixed(1)}%`;
                }
                if (sum === 0) {
                    meanScore = (0).toFixed(1);
                }
            }

            setStat("mean", sampleId, meanScore);
            setStat("labs", sampleId, labsReporting);
            setStat("count", sampleId, String(participantCount));
            setStat("percent", sampleId, percentageCorrect);
        });
    }

    each("select[data-amend-rating]", function (select) {
        select.addEventListener("change", statsVisible);
    });

    recalculate.addEventListener("click", function () {
        calculateStats();
        if (status) {
            status.textContent = "The statistics have been recalculated.";
        }
    });

    // Legacy recalculated on every page load, so a redisplayed page reflects the posted ratings.
    calculateStats();
})();

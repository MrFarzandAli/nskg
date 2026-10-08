using System.Text;

namespace Nskg.Helpers
{
    public static class ReportPaginationHelper
    {
        public static string GetPaginationStyles()
        {
            return @"
            /* Pagination Toolbar (Sticky at Top) */
            .rpt-toolbar {
                position: sticky;
                top: 0;
                z-index: 1000;
                background: #ffffff;
                border-bottom: 2px solid #0d6efd;
                padding: 8px 12px;
                display: flex;
                align-items: center;
                justify-content: space-between;
                box-shadow: 0 2px 6px rgba(0,0,0,0.1);
                margin: -12px -12px 12px -12px;
                font-family: Arial, sans-serif;
            }
            .rpt-toolbar-nav {
                display: flex;
                align-items: center;
                gap: 5px;
            }
            .rpt-btn {
                background: #f8f9fa;
                border: 1px solid #ced4da;
                border-radius: 4px;
                padding: 4px 10px;
                font-size: 12px;
                font-weight: 600;
                color: #212529;
                cursor: pointer;
                transition: all 0.2s;
                display: inline-flex;
                align-items: center;
                gap: 3px;
            }
            .rpt-btn:hover:not(:disabled) {
                background: #e9ecef;
                border-color: #adb5bd;
            }
            .rpt-btn:disabled {
                opacity: 0.45;
                cursor: not-allowed;
            }
            .rpt-select, .rpt-input {
                padding: 4px 6px;
                border: 1px solid #ced4da;
                border-radius: 4px;
                font-size: 12px;
                outline: none;
            }
            .rpt-select:focus, .rpt-input:focus {
                border-color: #86b7fe;
                box-shadow: 0 0 0 2px rgba(13,110,253,.25);
            }
            .rpt-page-info {
                font-size: 12px;
                font-weight: bold;
                color: #495057;
                display: inline-flex;
                align-items: center;
                gap: 4px;
            }
            .rpt-right-tools {
                display: flex;
                align-items: center;
                gap: 8px;
            }

            @media print {
                .rpt-toolbar, .no-print {
                    display: none !important;
                }
                body {
                    margin: 0 !important;
                    padding: 0 !important;
                }
                tr {
                    display: table-row !important;
                }
                tfoot {
                    display: table-footer-group !important;
                }
            }
            ";
        }

        public static string GetPaginationToolbarHtml(string title = "Report")
        {
            return @"
            <div class='rpt-toolbar no-print'>
                <div class='rpt-toolbar-nav'>
                    <button type='button' class='rpt-btn' id='btnFirst' onclick='rptGoFirst()' title='First Page'>&#171; First</button>
                    <button type='button' class='rpt-btn' id='btnPrev' onclick='rptGoPrev()' title='Previous Page'>&#8249; Prev</button>
                    <span class='rpt-page-info'>
                        Page
                        <select id='rptPageSelect' class='rpt-select' onchange='rptOnSelectPage(this.value)'></select>
                        of <span id='rptTotalPages'>1</span>
                    </span>
                    <button type='button' class='rpt-btn' id='btnNext' onclick='rptGoNext()' title='Next Page'>Next &#8250;</button>
                    <button type='button' class='rpt-btn' id='btnLast' onclick='rptGoLast()' title='Last Page'>Last &#187;</button>
                    
                    <span style='margin-left: 10px; font-size: 11px; color: #6c757d;'>
                        Rows/Page:
                        <select id='rptPageSize' class='rpt-select' onchange='rptChangePageSize(this.value)'>
                            <option value='50'>50</option>
                            <option value='100' selected>100</option>
                            <option value='200'>200</option>
                            <option value='500'>500</option>
                            <option value='-1'>All</option>
                        </select>
                    </span>
                    <span id='rptRecordStats' style='font-size: 11px; color: #495057; margin-left: 8px; font-weight: bold;'></span>
                </div>
                <div class='rpt-right-tools'>
                    <button type='button' class='rpt-btn' onclick='window.print()' style='background:#0d6efd; color:#fff; border-color:#0d6efd;'>
                        &#128438; Print
                    </button>
                </div>
            </div>";
        }

        public static string GetPaginationScript()
        {
            return @"
            <script>
                var rptCurrentPage = 1;
                var rptPageSize = 100;
                var rptTotalRows = 0;
                var rptTotalPages = 1;
                var rptRows = [];

                function rptInitPagination() {
                    var tbody = document.querySelector('table tbody');
                    if (!tbody) return;
                    rptRows = Array.from(tbody.querySelectorAll('tr'));
                    rptTotalRows = rptRows.length;
                    rptCalcPages();
                    rptRenderPage();
                }

                function rptCalcPages() {
                    if (rptPageSize === -1 || rptTotalRows <= 0) {
                        rptTotalPages = 1;
                    } else {
                        rptTotalPages = Math.ceil(rptTotalRows / rptPageSize);
                    }
                    if (rptCurrentPage > rptTotalPages) rptCurrentPage = rptTotalPages;
                    if (rptCurrentPage < 1) rptCurrentPage = 1;

                    var select = document.getElementById('rptPageSelect');
                    if (select) {
                        select.innerHTML = '';
                        for (var i = 1; i <= rptTotalPages; i++) {
                            var opt = document.createElement('option');
                            opt.value = i;
                            opt.textContent = i;
                            if (i === rptCurrentPage) opt.selected = true;
                            select.appendChild(opt);
                        }
                    }

                    var totalSpan = document.getElementById('rptTotalPages');
                    if (totalSpan) totalSpan.textContent = rptTotalPages;

                    var stats = document.getElementById('rptRecordStats');
                    if (stats) {
                        if (rptTotalRows === 0) {
                            stats.textContent = '(0 records)';
                        } else if (rptPageSize === -1) {
                            stats.textContent = '(All ' + rptTotalRows + ' records)';
                        } else {
                            var start = (rptCurrentPage - 1) * rptPageSize + 1;
                            var end = Math.min(rptCurrentPage * rptPageSize, rptTotalRows);
                            stats.textContent = '(' + start + '-' + end + ' of ' + rptTotalRows + ')';
                        }
                    }

                    var btnFirst = document.getElementById('btnFirst');
                    var btnPrev = document.getElementById('btnPrev');
                    var btnNext = document.getElementById('btnNext');
                    var btnLast = document.getElementById('btnLast');
                    if (btnFirst) btnFirst.disabled = (rptCurrentPage <= 1);
                    if (btnPrev) btnPrev.disabled = (rptCurrentPage <= 1);
                    if (btnNext) btnNext.disabled = (rptCurrentPage >= rptTotalPages);
                    if (btnLast) btnLast.disabled = (rptCurrentPage >= rptTotalPages);
                }

                function rptRenderPage() {
                    if (rptTotalRows <= 0) return;
                    var startIndex = (rptPageSize === -1) ? 0 : (rptCurrentPage - 1) * rptPageSize;
                    var endIndex = (rptPageSize === -1) ? rptTotalRows : startIndex + rptPageSize;

                    // If row 0 is opening balance, preserve it or handle appropriately
                    for (var i = 0; i < rptTotalRows; i++) {
                        if (i >= startIndex && i < endIndex) {
                            rptRows[i].style.display = '';
                        } else {
                            rptRows[i].style.display = 'none';
                        }
                    }

                    var select = document.getElementById('rptPageSelect');
                    if (select) select.value = rptCurrentPage;

                    var stats = document.getElementById('rptRecordStats');
                    if (stats) {
                        if (rptPageSize === -1) {
                            stats.textContent = '(All ' + rptTotalRows + ' records)';
                        } else {
                            var start = startIndex + 1;
                            var end = Math.min(endIndex, rptTotalRows);
                            stats.textContent = '(' + start + '-' + end + ' of ' + rptTotalRows + ')';
                        }
                    }

                    var btnFirst = document.getElementById('btnFirst');
                    var btnPrev = document.getElementById('btnPrev');
                    var btnNext = document.getElementById('btnNext');
                    var btnLast = document.getElementById('btnLast');
                    if (btnFirst) btnFirst.disabled = (rptCurrentPage <= 1);
                    if (btnPrev) btnPrev.disabled = (rptCurrentPage <= 1);
                    if (btnNext) btnNext.disabled = (rptCurrentPage >= rptTotalPages);
                    if (btnLast) btnLast.disabled = (rptCurrentPage >= rptTotalPages);

                    // Scroll report container to top
                    window.scrollTo({ top: 0, behavior: 'smooth' });
                }

                function rptGoFirst() {
                    if (rptCurrentPage !== 1) {
                        rptCurrentPage = 1;
                        rptRenderPage();
                    }
                }

                function rptGoPrev() {
                    if (rptCurrentPage > 1) {
                        rptCurrentPage--;
                        rptRenderPage();
                    }
                }

                function rptGoNext() {
                    if (rptCurrentPage < rptTotalPages) {
                        rptCurrentPage++;
                        rptRenderPage();
                    }
                }

                function rptGoLast() {
                    if (rptCurrentPage !== rptTotalPages) {
                        rptCurrentPage = rptTotalPages;
                        rptRenderPage();
                    }
                }

                function rptOnSelectPage(page) {
                    rptCurrentPage = parseInt(page, 10) || 1;
                    rptRenderPage();
                }

                function rptChangePageSize(size) {
                    rptPageSize = parseInt(size, 10);
                    rptCurrentPage = 1;
                    rptCalcPages();
                    rptRenderPage();
                }

                document.addEventListener('DOMContentLoaded', rptInitPagination);
                if (document.readyState === 'complete' || document.readyState === 'interactive') {
                    setTimeout(rptInitPagination, 50);
                }
            </script>
            ";
        }
    }
}

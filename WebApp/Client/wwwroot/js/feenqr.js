// FeenQR shared JavaScript helpers
window.feenqr = {
  // Track Chart.js instances to avoid duplicates
  _chartInstances: {},

  destroyChart: function (canvasId) {
    if (this._chartInstances[canvasId]) {
      this._chartInstances[canvasId].destroy();
      delete this._chartInstances[canvasId];
    }
  },

  renderTechCharts: function (dto) {
    try {
      // Destroy existing chart instances
      this.destroyChart("priceChart");
      this.destroyChart("rsiChart");

      // --- Price & Moving Averages Chart ---
      var priceCtx = document.getElementById("priceChart");
      if (!priceCtx) {
        console.warn("priceChart canvas not found");
        return;
      }

      var priceChart = new Chart(priceCtx, {
        type: "line",
        data: {
          labels: dto.dates,
          datasets: [
            {
              label: "Close",
              data: dto.close,
              borderColor: "#007bff",
              backgroundColor: "rgba(0, 123, 255, 0.1)",
              fill: false,
              borderWidth: 2,
              pointRadius: 1,
              pointHoverRadius: 4
            },
            {
              label: "SMA 20",
              data: dto.sma20,
              borderColor: "#6c757d",
              fill: false,
              borderWidth: 1.5,
              pointRadius: 0,
              borderDash: [4, 2]
            },
            {
              label: "SMA 50",
              data: dto.sma50,
              borderColor: "#28a745",
              fill: false,
              borderWidth: 1.5,
              pointRadius: 0,
              borderDash: [4, 2]
            },
            {
              label: "SMA 200",
              data: dto.sma200,
              borderColor: "#dc3545",
              fill: false,
              borderWidth: 1.5,
              pointRadius: 0,
              borderDash: [4, 2]
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          interaction: {
            intersect: false,
            mode: "index"
          },
          plugins: {
            legend: {
              position: "top",
              labels: { color: "#ccc", usePointStyle: true }
            }
          },
          scales: {
            x: {
              ticks: {
                color: "#999",
                maxTicksLimit: 10,
                maxRotation: 45
              },
              grid: { color: "rgba(255,255,255,0.05)" }
            },
            y: {
              ticks: { color: "#999" },
              grid: { color: "rgba(255,255,255,0.05)" }
            }
          }
        }
      });
      this._chartInstances["priceChart"] = priceChart;

      // --- RSI / MACD Combined Chart ---
      var rsiCtx = document.getElementById("rsiChart");
      if (!rsiCtx) {
        console.warn("rsiChart canvas not found");
        return;
      }

      var rsiChart = new Chart(rsiCtx, {
        type: "line",
        data: {
          labels: dto.dates,
          datasets: [
            {
              label: "RSI",
              data: dto.rsi,
              borderColor: "#ff9900",
              backgroundColor: "rgba(255, 153, 0, 0.1)",
              fill: false,
              borderWidth: 2,
              pointRadius: 1,
              pointHoverRadius: 4,
              yAxisID: "y-rsi"
            },
            {
              label: "MACD",
              data: dto.macd,
              borderColor: "#007bff",
              fill: false,
              borderWidth: 1.5,
              pointRadius: 0,
              yAxisID: "y-macd"
            },
            {
              label: "MACD Signal",
              data: dto.macdSignal,
              borderColor: "#dc3545",
              fill: false,
              borderWidth: 1.5,
              pointRadius: 0,
              borderDash: [4, 2],
              yAxisID: "y-macd"
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          interaction: {
            intersect: false,
            mode: "index"
          },
          plugins: {
            legend: {
              position: "top",
              labels: { color: "#ccc", usePointStyle: true }
            }
          },
          scales: {
            x: {
              ticks: {
                color: "#999",
                maxTicksLimit: 10,
                maxRotation: 45
              },
              grid: { color: "rgba(255,255,255,0.05)" }
            },
            "y-rsi": {
              type: "linear",
              display: true,
              position: "left",
              min: 0,
              max: 100,
              ticks: { color: "#ff9900" },
              grid: { color: "rgba(255,255,255,0.05)" },
              title: {
                display: true,
                text: "RSI",
                color: "#ff9900"
              }
            },
            "y-macd": {
              type: "linear",
              display: true,
              position: "right",
              ticks: { color: "#007bff" },
              grid: { drawOnChartArea: false },
              title: {
                display: true,
                text: "MACD",
                color: "#007bff"
              }
            }
          }
        }
      });
      this._chartInstances["rsiChart"] = rsiChart;

    } catch (error) {
      console.error("Error rendering tech charts:", error);
    }
  },

  exportPitchPdf: async function (containerId, fileName) {
    try {
      var element = document.getElementById(containerId);
      if (!element) {
        console.error("Container element not found:", containerId);
        return;
      }

      var canvas = await html2canvas(element, {
        scale: 2,
        useCORS: true,
        logging: false,
        backgroundColor: "#000000"
      });

      var imgData = canvas.toDataURL("image/png");
      var pdf = new window.jspdf.jsPDF("p", "pt", "a4");
      var imgWidth = 550;
      var pageHeight = 780;
      var imgHeight = (canvas.height * imgWidth) / canvas.width;
      var heightLeft = imgHeight;
      var position = 20;

      pdf.addImage(imgData, "PNG", 30, position, imgWidth, imgHeight);
      heightLeft -= pageHeight;

      while (heightLeft > 0) {
        position = heightLeft - imgHeight;
        pdf.addPage();
        pdf.addImage(imgData, "PNG", 30, position, imgWidth, imgHeight);
        heightLeft -= pageHeight;
      }

      pdf.save(fileName);
    } catch (error) {
      console.error("Error exporting PDF:", error);
    }
  },

  // Scroll a container's first child into view (used for active tab)
  scrollIntoView: function (elementId) {
    var el = document.getElementById(elementId);
    if (el) el.scrollIntoView({ behavior: "smooth", block: "start" });
  }
};

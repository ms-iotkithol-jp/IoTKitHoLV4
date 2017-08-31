$(document).ready(function () {
    var timeData = [],
        tempData = [];
    var data = {
        labels: timeData,
        datasets: [
            {
                fill: false,
                label: 'Environment',
                yAxisID: 'Temperature',
                borderColor: "rgba(255, 204, 0, 1)",
                pointBoarderColor: "rgba(255, 204, 0, 1)",
                backgroundColor: "rgba(255, 204, 0, 0.4)",
                pointHoverBackgroundColor: "rgba(255, 204, 0, 1)",
                pointHoverBorderColor: "rgba(255, 204, 0, 1)",
                data: tempData
            }
        ]
    }

    var basicOption = {
        title: {
            display: true,
            text: 'Envrionment Real-time Data',
            fontSize: 36
        },
        scales: {
            yAxes: [{
                id: 'Temperature',
                type: 'linear',
                scaleLabel: {
                    labelString: 'Temperature',
                    display: true
                },
                position: 'left',
            }]
        }
    }

    //Get the context of the canvas element we want to select
    var ctx = document.getElementById("myChart").getContext("2d");
    var optionsNoAnimation = { animation: false }
    var myLineChart = new Chart(ctx, {
        type: 'line',
        data: data,
        options: basicOption
    });
    var hub = $.connection.envHub;
    hub.on("Update", function (packet) {
        if (!packet.Time || !packet.Temperature) {
            return;
        }
        timeData.push(packet.Time);
        tempData.push(packet.Temperature);

        // only keep no more than 50 points in the line chart
        var len = timeData.length;
        if (len > 50) {
            timeData.shift();
            tempData.shift();
        }
        myLineChart.update();
    });
    $.connection.hub.start();
});

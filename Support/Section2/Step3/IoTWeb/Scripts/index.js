$(document).ready(function () {
    var timeData = [],
        tempData = [],
        presData = [];
    var dataTempG = {
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

    var dataPresG = {
        labels: timeData,
        datasets: [
            {
                fill: false,
                label: 'Environment',
                yAxisID: 'Pressure',
                borderColor: "rgba(2, 204, 0, 1)",
                pointBoarderColor: "rgba(2, 204, 0, 1)",
                backgroundColor: "rgba(2, 204, 0, 0.4)",
                pointHoverBackgroundColor: "rgba(2, 204, 0, 1)",
                pointHoverBorderColor: "rgba(2, 204, 0, 1)",
                data: presData
            }
        ]
    }

    var basicOptionTemp = {
        title: {
            display: true,
            text: 'Envrionment Real-time Temperature Data',
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

    var basicOptionPress = {
        title: {
            display: true,
            text: 'Envrionment Real-time Pressure Data',
            fontSize: 36
        },
        scales: {
            yAxes: [{
                id: 'Pressure',
                type: 'linear',
                scaleLabel: {
                    labelString: 'Pressure',
                    display: true
                },
                position: 'left',
            }]
        }
    }

    //Get the context of the canvas element we want to select
    var ctxTemp = document.getElementById("tempChart").getContext("2d");
    var optionsNoAnimation = { animation: false }
    var tempLineChart = new Chart(ctxTemp, {
        type: 'line',
        data: dataTempG,
        options: basicOptionTemp
    });
    var ctxPress = document.getElementById("pressChart").getContext("2d");
    var presLineChart = new Chart(ctxPress, {
        type: 'line',
        data: dataPresG,
        options: basicOptionPress
    });
    var hub = $.connection.envHub;
    hub.on("Environment", function (packet) {
        if (!packet.time || !packet.temperature || !packet.pressure) {
            return;
        }
        timeData.push(packet.time);
        tempData.push(packet.temperature);
        presData.push(packet.pressure);

        // only keep no more than 50 points in the line chart
        var len = timeData.length;
        if (len > 200) {
            timeData.shift();
            tempData.shift();
            presData.shift();
        }
        tempLineChart.update();
        presLineChart.update();
    });
    $.connection.hub.start();
});

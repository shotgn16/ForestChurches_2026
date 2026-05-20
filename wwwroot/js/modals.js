$(document).ready(function () {
    if (document.getElementById('showPlacesModal').value === 'true') {
        $('#churchesModal').modal('show');
    }
    if (document.getElementById('showEventsModal').value === 'true') {
        $('#eventsModal').modal('show');
    }
});

$(document).ready(function () {
    var message = document.getElementById('tempDataMessage').value;
    if (message) {
        alert(message);
    }
});

$(document).ready(function () {
    loadDoctorAppointments();
    $('#filterStatus').change(function () {
        $('#doctorAppointmentsTable').DataTable().ajax.reload();
    });

});

function loadDoctorAppointments() {
    $('#doctorAppointmentsTable').DataTable({
        processing: true,
        serverSide: true,
        destroy: true,
        ajax: {
            url: '/DoctorAppointment/GetMyAppointments',
            type: 'POST',
            data: function (d) {
                d.status = $('#filterStatus').val(); 
            }
        },
        columns: [
            { data: 'patientName', title: 'Patient' },
            { data: 'appointmentDate', title: 'Date' },
            { data: 'description', title: 'Description' },
            { data: 'status', title: 'Status' },
            {
                data: 'id',
                title: 'Update Status',
                render: function (id, type, row) {
                    return `
                        <form class="update-status-form" data-id="${id}">
                            <select class="form-select form-select-sm status-select">
                                <option value="Pending" ${row.status === "Pending" ? "selected" : ""}>Pending</option>
                                <option value="Confirmed" ${row.status === "Confirmed" ? "selected" : ""}>Confirmed</option>
                                <option value="Cancelled" ${row.status === "Cancelled" ? "selected" : ""}>Cancelled</option>
                            </select>
                        </form>
                    `;
                }
            }
        ]
    });
}


$(document).on('change', '.status-select', function () {
    const form = $(this).closest('.update-status-form');
    const id = form.data('id');
    const newStatus = $(this).val();

    $.ajax({
        url: '/DoctorAppointment/UpdateStatus',
        method: 'POST',
        data: {
            id: id,
            status: newStatus
        },
        success: function (res) {
            if (res.success) {
                $('#doctorAppointmentsTable').DataTable().ajax.reload(null, false);
                showStatusMessage("Status updated successfully!", "success");
            } else {
                showStatusMessage("Failed to update status.", "danger");
            }
        },
        error: function () {
            showStatusMessage("An error occurred while updating the status.", "danger");
        }

    });
});

function showStatusMessage(message, type) {
    $('#statusMessage')
        .removeClass('d-none alert-success alert-danger')
        .addClass(`alert-${type}`)
        .text(message);

    setTimeout(() => {
        $('#statusMessage').addClass('d-none').removeClass(`alert-${type}`);
    }, 3000); // Hide after 3 seconds
}

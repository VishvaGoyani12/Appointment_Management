var originalModal = $("#divModal").clone();

$("#divModal").on("hidden.bs.modal", function () {
    $("#divModal").remove();
    const myClone = originalModal.clone();
    $("body").append(myClone);
});

$(document).ready(function () {
    loadData();

    $('#btnAdd').click(function () {
        openAppointmentModal('/Appointment/Create');
    });

    $('#filterPatient, #filterDoctor, #filterStatus').change(function () {
        $('#appointmentTable').DataTable().ajax.reload();
    });
});

function openAppointmentModal(url) {
    $("#modalContent").load(url, function () {
        showModal();
    });
}

function showModal() {
    $("#divModal").modal('show');
}

function closeModal() {
    $('#modalContent').html('');
    $('#divModal').modal('hide');
}

$(document).on('submit', '#formCreateOrEdit', function (e) {
    e.preventDefault();

    var form = $(this);
    var url = form.attr('action');
    var formData = form.serialize();

    $.ajax({
        type: "POST",
        url: url,
        data: formData,
        success: function (response) {
            if (response.success) {
                closeModal();
                showToast('success', response.message || 'Operation successful');
                $('#appointmentTable').DataTable().ajax.reload(null, false);
            } else {
                if (typeof response === 'string') {
                    $('#modalContent').html(response);
                } else {
                    showToast('error', response.message || 'An error occurred');
                }
            }
        },
        error: function () {
            showToast('error', 'An unexpected error occurred.');
        }
    });
});


function loadData() {
    $('#appointmentTable').DataTable({
        processing: true,
        serverSide: true,
        destroy: true,
        ajax: {
            url: '/Appointment/GetAll',
            type: 'POST',
            data: function (d) {
                d.patientId = $('#filterPatient').val();
                d.doctorId = $('#filterDoctor').val();
                d.status = $('#filterStatus').val();
            }
        },
        columns: [
            { data: 'patientName', title: 'Patient' },
            { data: 'doctorName', title: 'Doctor' },
            { data: 'appointmentDate', title: 'Date' },
            { data: 'description', title: 'Description' },
            { data: 'status', title: 'Status' },
            {
                data: 'id', title: 'Actions',
                render: function (data) {
                    return `
                        <button onclick="editAppointment(${data})" class="btn btn-sm btn-warning">Edit</button>
                        <button onclick="deleteAppointment(${data})" class="btn btn-sm btn-danger">Delete</button>`;
                }
            }
        ]
    });
}

function editAppointment(id) {
    openAppointmentModal(`/Appointment/Edit/${id}`);
}

function deleteAppointment(id) {
    if (confirm("Are you sure to delete this appointment?")) {
        $.post('/Appointment/Delete', { id: id }, function (res) {
            if (res.success) {
                loadData();
            }
        });
    }
}

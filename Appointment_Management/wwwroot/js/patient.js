var originalModal = $("#divModal").clone();

$("#divModal").on("hidden.bs.modal", function () {
    $("#divModal").remove();
    const myClone = originalModal.clone();
    $("body").append(myClone);
});

$(document).ready(function () {
    loadData();

    $('#filterGender, #filterStatus').change(function () {
        $('#patientTable').DataTable().ajax.reload();
    });

    $('#filterJoinDate').on('input', function () {
        $('#patientTable').DataTable().ajax.reload();
    });
});

function openPatientModal(url) {
    $("#modalContent").load(url, function () {
        prepareModalFields();
        showModal();
    });
}

function prepareModalFields() {
    $("#modalContent input, #modalContent select").each(function () {
        var name = $(this).attr("name");
        if (name === "Status") {
            $(this).prop("disabled", false);
        } else {
            if ($(this).is("input")) {
                $(this).prop("readonly", true);
            } else if ($(this).is("select")) {
                $(this).prop("disabled", true);
            }
        }
    });
    $(".modal-footer button[type=submit]").show();
}

function showModal() {
    $("#divModal").modal('show');
}

$(document).on('submit', '#formCreateOrEdit', function (e) {
    e.preventDefault();
    var form = $(this);
    var token = form.find('input[name="__RequestVerificationToken"]').val();
    var patientId = form.find('input[name="Id"]').val();
    var isEdit = patientId && !isNaN(parseInt(patientId)) && parseInt(patientId) > 0;

    if (!isEdit) {
        alert("You are not authorized to add patients.");
        return;
    }

    $.ajax({
        url: '/Patient/Edit',
        type: 'POST',
        data: form.serialize(),
        headers: { 'RequestVerificationToken': token },
        success: function (res) {
            if (res.success) {
                closeModal();
                loadData();
            } else {
                $('#modalContent').html(res);
                prepareModalFields(); 
            }
        },
        error: function (xhr) {
            console.error("Save error:", xhr.responseText);
        }
    });
});

function closeModal() {
    $('#modalContent').html('');
    $('#divModal').modal('hide');
}

function editPatient(id) {
    openPatientModal(`/Patient/Edit/${id}`);
}


function loadData() {
    $('#patientTable').DataTable({
        processing: true,
        serverSide: true,
        destroy: true,
        ajax: {
            url: '/Patient/GetAll',
            type: 'POST',
            data: function (d) {
                d.gender = $('#filterGender').val();
                d.status = $('#filterStatus').val();
                d.joinDate = $('#filterJoinDate').val();
            }
        },
        columns: [
            { data: 'name', title: 'Name' },
            { data: 'gender', title: 'Gender' },
            { data: 'joinDate', title: 'Join Date' },
            { data: 'status', title: 'Status' },
            {
                data: 'id', title: 'Actions',
                render: function (data) {
                    return `<button onclick="editPatient(${data})" class="btn btn-sm btn-warning">Edit Status</button>`;
                }
            }
        ]
    });
}

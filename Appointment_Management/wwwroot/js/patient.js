var originalModal = $("#divModal").clone();

$("#divModal").on("hidden.bs.modal", function () {
    $("#divModal").remove();
    const myClone = originalModal.clone();
    $("body").append(myClone);
});

$(document).ready(function () {
    loadData();

    // Removed Add button click handler as admin cannot add patients
    // Removed filterJoinDate input event since it is optional, but keep if you want

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
    // Make all inputs and selects readonly/disabled except the Status field
    $("#modalContent input, #modalContent select").each(function () {
        var name = $(this).attr("name");
        if (name === "Status") {
            $(this).prop("disabled", false);
        } else {
            // For inputs, make readonly; for selects, disable
            if ($(this).is("input")) {
                $(this).prop("readonly", true);
            } else if ($(this).is("select")) {
                $(this).prop("disabled", true);
            }
        }
    });

    // Show Save button only (modal footer) if editing (disable for add if needed)
    // We assume admin only edits, so always show save.
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

    // Only allow POST to Edit endpoint since admin cannot create
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
                prepareModalFields(); // reapply readonly/disable after partial reload
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

// Remove deletePatient function and buttons from table since admin cannot delete

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
                    // Only Edit button, no Delete or Add
                    return `<button onclick="editPatient(${data})" class="btn btn-sm btn-warning">Edit Status</button>`;
                }
            }
        ]
    });
}

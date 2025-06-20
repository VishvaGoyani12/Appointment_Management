﻿var originalModal = $("#divModal").clone();

$("#divModal").on("hidden.bs.modal", function () {
    $("#divModal").remove();
    const myClone = originalModal.clone();
    $("body").append(myClone);
});

// Include auth.js first in your HTML
$(document).ready(function () {
    loadSpecialistFilter();
    loadData();

    $('#btnAdd').click(function () {
        openDoctorModal('/Doctor/Create');
    });

    $('#filterGender, #filterStatus, #filterSpecialistIn').change(function () {
        var table = $('#doctorTable').DataTable();
        table.ajax.reload();
    });
});


function openDoctorModal(url) {
    $("#modalContent").load(url, function (response, status, xhr) {
        if (status === "error") {
            console.error("Failed to load modal content:", xhr.responseText);
        } else {
            showModal();
        }
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
    var token = form.find('input[name="__RequestVerificationToken"]').val();
    var doctorId = form.find('input[name="ApplicationUserId"]').val();
    var isEdit = doctorId && doctorId.trim() !== '';

    $.ajax({
        url: isEdit ? '/Doctor/Edit' : '/Doctor/Create',
        type: 'POST',
        data: form.serialize(),
        headers: {
            'RequestVerificationToken': token
        },
        success: function (res) {
            if (typeof res === 'object') {
                if (res.success) {
                    closeModal();
                    loadData();
                    toastr.success("Doctor saved successfully.");
                } else {
                    toastr.error(res.message || "An error occurred while saving the doctor.");
                }
            } else {
                $('#modalContent').html(res);
            }
        },
        error: function (xhr) {
            toastr.error("Unexpected error. Please try again.");
            console.error("AJAX Error:", xhr.status, xhr.responseText);
        }
    });
});




function loadSpecialistFilter() {
    $.getJSON('/Doctor/GetSpecialistList', function (data) {
        var select = $('#filterSpecialistIn');
        $.each(data, function (i, item) {
            select.append($('<option>', {
                value: item,
                text: item
            }));
        });
    });
}

function editDoctor(id) {
    openDoctorModal(`/Doctor/Edit/${id}`);
}

function deleteDoctor(id) {
    if (!confirm("Are you sure you want to delete this doctor?")) return;

    $.ajax({
        url: '/Doctor/Delete',
        type: 'POST',
        data: { id: id },
        success: function (response) {
            if (response.success) {
                toastr.success(response.message);
                $('#doctorTable').DataTable().ajax.reload();
            } else {
                toastr.error(response.message);
            }
        },
        error: function (xhr) {
            let msg = "An error occurred while deleting the doctor.";
            if (xhr.responseJSON && xhr.responseJSON.message) {
                msg = xhr.responseJSON.message;
            }
            toastr.error(msg);
        }
    });
}



function loadData() {
    var table = $('#doctorTable').DataTable({
        processing: true,
        serverSide: true,
        destroy: true, 
        ajax: {
            url: '/Doctor/GetAll',
            type: 'POST',
            data: function (d) {
                d.gender = $('#filterGender').val();
                d.status = $('#filterStatus').val();
                d.specialistIn = $('#filterSpecialistIn').val();
            },
            error: function (xhr, error, thrown) {
                if (xhr.responseJSON && xhr.responseJSON.error) {
                    toastr.error(xhr.responseJSON.error);
                } else {
                    toastr.error("Error loading doctor data");
                }
            }
        },
        columns: [
            { data: 'fullName', title: 'Full Name' },
            { data: 'gender', title: 'Gender' },
            { data: 'specialistIn', title: 'Specialist In' },
            { data: 'status', title: 'Status' },
            {
                data: 'id',
                title: 'Actions',
                orderable: false,
                render: function (data) {
                    return `
                        <button onclick="editDoctor('${data}')" class="btn btn-sm btn-warning">Edit</button>
                        <button onclick="deleteDoctor('${data}')" class="btn btn-sm btn-danger">Delete</button>
                    `;
                }
            }
        ]
    });

    return table;
}

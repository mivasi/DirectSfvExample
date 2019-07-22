const options = {
    env: 'Local'
};

async function createWorkItem() {
    var input = document.getElementById("uploadFileInput");
    var files = input.files;
    var formData = new FormData();

    for (var i = 0; i != files.length; i++) {
        formData.append("files", files[i]);
    }

    return await $.ajax({
        url: "api/da/workitem",
        type: "POST",
        data: formData,
        contentType: false,
        processData: false
    });
}

async function monitorWorkitem(workitemStatus) {
    while (workitemStatus.status === "pending" || workitemStatus.status === "inprogress") {
        console.log("Checking workitem status");
        workitemStatus = await $.ajax({
            url: "api/da/workitem/" + workitemStatus.id,
            type: "GET"
        });
        await promiseTimeout(500);
    }

    return workitemStatus;
}

async function downloadResults(workitemWithStatus) {
    return await $.ajax({
        url: "api/da/downloads",
        type: "POST",
        data: JSON.stringify(workitemWithStatus),
        contentType: "application/json"
    });
}

function showModelInViewer(workitemId) {
    let documentURL = 'results/' + workitemId + '/bubble.json';
    let viewer = new Autodesk.Viewing.GuiViewer3D(document.getElementById('ForgeViewer'));

    Autodesk.Viewing.Initializer(options, function () {

        var startedCode = viewer.start();
        if (startedCode > 0) {
            console.error('Failed to create a Viewer: WebGL not supported.');
            return;
        }

        console.log('Initialization complete, loading a model next...');

        Autodesk.Viewing.Document.load(documentURL, onDocumentLoadSuccess, onDocumentLoadFailure);

        function onDocumentLoadSuccess(viewerDocument) {
            var defaultModel = viewerDocument.getRoot().getDefaultGeometry();
            viewer.loadDocumentNode(viewerDocument, defaultModel);
        }

        function onDocumentLoadFailure() {
            console.error('Failed fetching Forge manifest');
        }
    });
}

function promiseTimeout(time) {
    return new Promise(function (resolve, reject) {
        setTimeout(resolve, time);
    });
}

$(document).ready(function (e) {
    $("#uploadFile").on('submit', (async function (e) {
        e.preventDefault();
        try {
            let workitemWithStatus = await createWorkItem();
            let workitemStatusDone = await monitorWorkitem(workitemWithStatus.status);
            console.log(workitemStatusDone);
            await downloadResults(workitemWithStatus);
            showModelInViewer(workitemWithStatus.status.id);
        } catch (exception) {
            console.log(exception);
        }
    }));
});

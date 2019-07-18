const options = {
    env: 'Local'
};

let modelURL = 'model/1/result.svf';
let viewer = new Autodesk.Viewing.GuiViewer3D(document.getElementById('MyViewerDiv'));

Autodesk.Viewing.Initializer(options, function () {

    var startedCode = viewer.start(modelURL, options);
    if (startedCode > 0) {
        console.error('Failed to create a Viewer: WebGL not supported.');
        return;
    }

});
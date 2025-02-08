function WebPicAutoController($scope, $http, umbRequestHelper) {
    let vm = this;
    let baseApiUrl = "backoffice/Api/WebPicAuto/";

    $scope.selectedImages = new Set();
    $scope.resizeExisting = true;
    $scope.convertExisting = true;

    $scope.getSettings = () => {

        umbRequestHelper.resourcePromise($http.get(baseApiUrl + "GetSettings"))

            .then((responseData) => { 
                vm.settings = responseData;
                vm.sizeSavedConverting = $scope.humanFileSize(vm.settings.WpaBytesSavedConverting);
                vm.sizeSavedResizing = $scope.humanFileSize(vm.settings.WpaBytesSavedResizing);
            });
    }

    //Saves WebPic settings on the server
    $scope.saveSettings = () => {
        umbRequestHelper.resourcePromise(
                $http.post(baseApiUrl + "SetSettings", JSON.stringify(vm.settings))
            )
            .then((response) => {
                $scope.addToast("Success", response.message, "positive");
            })
            .catch((error) => {
                $scope.addToast("Oops!", error.errorMsg, "danger");
            });
            
    }

    $scope.setProperty = (propName, value) => {
        vm.settings[propName] = value;
    };

    $scope.toggle = (propName) => {
        let currentValue = vm.settings[propName];
        vm.settings[propName] = !currentValue;
    };

    $scope.selectImage = (element) => {

        let overlay = element.children[0];

        if(!$scope.selectedImages.has(element.id)){
            $scope.selectedImages.add(element.id);

            if(overlay)
                overlay.classList.add("wpaSelectedImg");
        }
        else{
            $scope.selectedImages.delete(element.id);
        
            if (overlay)
                overlay.classList.remove("wpaSelectedImg");
        }
    }

    $scope.selectAllImages = () => {

        var elements = document.getElementsByClassName("wpaSelectableImg");
        var btn = document.getElementById("wpaSelectUnselectImages");

        var t = btn.innerHTML;

        //Unselect all images
        if (t == "Unselect all") {

            $scope.selectedImages.clear();//Remove 

            for (let i = 0; i < elements.length; i++) {
                elements[i].children[0].classList.remove("wpaSelectedImg");                
            }
            btn.innerHTML = "Select all";
        }
        //Select all images
        else if ( t == "Select all")  {

            for (let i = 0; i < elements.length; i++) {
                elements[i].children[0].classList.add("wpaSelectedImg");
                if (!$scope.selectedImages.has(elements[i].id)) {
                    $scope.selectedImages.add(elements[i].id);
                }
            }
            btn.innerHTML = "Unselect all";
        } 
    }

    $scope.toggleResizeExisting = () => {
        $scope.resizeExisting = !$scope.resizeExisting;
    }

    $scope.toggleConvertExisting = () => {
        $scope.convertExisting = !$scope.convertExisting;
    }

    $scope.closePopup = (popupId) => {
        var popup = document.getElementById(popupId);
        popup.hidePopover();
    }

    $scope.openPopup = (popupId) => {
        var popup = document.getElementById(popupId);
        popup.showPopover();
    }

    //Checks and returns all images that can be resized and/or converted
    $scope.checkMedia = () => {

        //Clear selection 
        $scope.selectedImages = new Set();

        umbRequestHelper.resourcePromise($http.get(baseApiUrl + "CheckMedia"))
            .then((responseData) => {
                vm.mediaCheckResults = responseData;
            })
            .catch((error) => {
                $scope.addToast("Oops!", error.errorMsg, "danger");
            });
    }


    $scope.addToast = (headline, message, color) => {
        const con = document.querySelector('uui-toast-notification-container');
        const toast = document.createElement('uui-toast-notification');
        toast.color = color;
        const toastLayout = document.createElement('uui-toast-notification-layout');
        toastLayout.headline = headline;
        toast.appendChild(toastLayout);

        const messageEl = document.createElement('span');
        messageEl.innerHTML = message;
        toastLayout.appendChild(messageEl);

        if (con) {
            con.appendChild(toast);
        }
    }


    $scope.processExistingImages = () => {

        $scope.closePopup('wpaProcessConfirm');

        var imageIds = Array.from($scope.selectedImages);

        if (imageIds.length > 0) {

            let optimizeBtn = document.getElementById('wpaProcessExistingBtn');
            optimizeBtn.setAttribute("state", "waiting");

            umbRequestHelper.resourcePromise($http.post(baseApiUrl + "ProcessExistingImages", { ids: imageIds, resize: $scope.resizeExisting, convert: $scope.convertExisting }))
                .then(function (responseData) {
                    console.log("Image Processing response: " + responseData);
                    if (responseData === 'Success') {

                        //Recheck media
                        $scope.checkMedia();
                        $scope.closePopup('wpaProcessConfirm');
                        optimizeBtn.removeAttribute("state");
                    }
                });

        }
        else {
            alert("You need to select some images first.");
        }
    }


    function init() {

        $scope.getSettings();
    }
    init();

    $scope.humanFileSize = (bytes, si = false, dp = 1) => {
        const thresh = si ? 1000 : 1024;

        if (Math.abs(bytes) < thresh) {
            return bytes + ' B';
        }

        const units = si
            ? ['kB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB']
            : ['KiB', 'MiB', 'GiB', 'TiB', 'PiB', 'EiB', 'ZiB', 'YiB'];

        let u = -1;
        const r = 10 ** dp;

        do {
            bytes /= thresh;
            ++u;
        } while (Math.round(Math.abs(bytes) * r) / r >= thresh && u < units.length - 1);
        return bytes.toFixed(dp) + ' ' + units[u];
    }
}

angular.module("umbraco").controller("Badgernet.Umbraco.WebPicAutoController", WebPicAutoController);
﻿/*jslint unparam: true, white: true, browser: true, devel: true */
/*global define, console */

define('bcms.media.upload', ['jquery', 'bcms', 'bcms.dynamicContent', 'bcms.modal', 'html5Upload', 'knockout'],
    function ($, bcms, dynamicContent, modal, html5Upload, ko) {
    'use strict';

    var mediaUpload = {},

    selectors = {
        dragZone: '#bcms-files-dropzone'
    },

    classes = {
        dragZoneActive: 'bcms-dropzone-active'
    },

    links = {
        loadUploadFilesDialogUrl: null,
        uploadFileToServerUrl: null,
        undoFileUploadUrl: null
    },

    globalization = {
        uploadFilesDialogTitle: null
    };

    /**
    * Assign objects to module.
    */
    mediaUpload.links = links;
    mediaUpload.globalization = globalization;    

    mediaUpload.openUploadFilesDialog = function (rootFolderId, rootFolderType, onSaveCallback) {
        var options = {
                uploads: new UploadsViewModel(),
                rootFolderId: rootFolderId,
                rootFolderType: rootFolderType
            };
            
        modal.open({
            title: globalization.uploadFilesDialogTitle,            
            onLoad: function (dialog) {
                var url = $.format(links.loadUploadFilesDialogUrl, rootFolderId, rootFolderType);
                dynamicContent.bindDialog(dialog, url, {
                    contentAvailable: function () {
                        initUploadFilesDialogEvents(dialog, options);
                    },

                    beforePost: function () {
                        dialog.container.showLoading();
                    },
                    
                    postSuccess: function(json) {
                        if (onSaveCallback && $.isFunction(onSaveCallback)) {
                            onSaveCallback(json);
                        }
                    },

                    postComplete: function () {
                        dialog.container.hideLoading();
                    }
                });
            },
            onCancel: function () {
                uploadsModel.removeAllUploads();
            }
        });
    };
        
    function UploadsViewModel() {
        var self = this,

            undoUpload = function (fileViewModel) {
                $.post($.format(links.undoFileUploadUrl, fileViewModel.fileId(), fileViewModel.version(), fileViewModel.type()));
            },
            
            abortUpload = function (fileViewModel) {
                self.uploads.remove(fileViewModel);
                
                if (self.activeUploads.indexOf(fileViewModel) !== -1) {
                    self.activeUploads.remove(fileViewModel);
                }
                
                if (fileViewModel.uploadCompleted() === false && fileViewModel.uploadFailed() === false) {
                    fileViewModel.file.abort();
                } else if (fileViewModel.uploadCompleted() === true) {
                    undoUpload(fileViewModel);
                }
            };
        
        self.uploads = ko.observableArray();
        self.activeUploads = ko.observableArray();
        
        self.cancelAllActiveUploads = function () {
            var uploads = self.activeUploads.removeAll();
            for (var i = 0; i < uploads.length; i++) {                                                
                abortUpload(uploads[i]);
            }
        };
        
        self.removeUpload = function (fileViewModel) {            
            abortUpload(fileViewModel);
        };

        self.removeAllUploads = function() {
            var uploads = self.uploads.removeAll();
            for (var i = 0; i < uploads.length; i++) {
                abortUpload(uploads[i]);
            }
        };
    } 

    function FileViewModel(file) {
        var self = this;
        self.file = file;
        self.fileId = ko.observable('');
        self.version = ko.observable(0);
        self.type = ko.observable(0);
        self.uploadProgress = ko.observable(0);
        self.uploadCompleted = ko.observable(false);
        self.uploadFailed = ko.observable(false);
        self.uploadSpeedFormatted = ko.observable();
        self.fileName = file.fileName;
        self.fileSizeFormated = formatFileSize(file.fileSize);        
    }
        
    function initUploadFilesDialogEvents(dialog, options) {
        var uploadsModel = options.uploads,
            dragZone = dialog.container.find(selectors.dragZone),
            cancelEvent = function (e) {
                e.preventDefault();
                e.stopPropagation();
            };
        
        dragZone.on("dragenter dragover", function (e) {
            cancelEvent(e);
            dragZone.addClass(classes.dragZoneActive);
            dragZone.children().hide();
            return false;
        });
        
        dragZone.on("dragleave drop", function (e) {
            cancelEvent(e);
            dragZone.removeClass(classes.dragZoneActive);
            dragZone.children().show();
            return false;
        });

        if (html5Upload.fileApiSupported()) {

            var context = document.getElementById('bcms-media-uploads');

            html5Upload.initialize({
                uploadUrl: links.uploadFileToServerUrl,
                dropContainer: document.getElementById('bcms-files-dropzone'),
                inputField: document.getElementById('bcms-files-upload-input'),
                key: 'File',
                data: { rootFolderId: options.rootFolderId, rootFolderType: options.rootFolderType },
                maxSimultaneousUploads: 4,
                onFileAdded: function (file) {
                    var fileModel = new FileViewModel(file);
                    uploadsModel.activeUploads.push(fileModel);
                    uploadsModel.uploads.push(fileModel);
                    
                    file.on({
                        // Called after received response from the server
                        onCompleted: function (data) {
                            var result = JSON.parse(data);
                            if (result.Success) {
                                uploadsModel.activeUploads.remove(fileModel);
                                fileModel.uploadCompleted(true);
                                fileModel.fileId(result.Data.FileId);
                                fileModel.version(result.Data.Version);
                                fileModel.type(result.Data.Type);
                            } else {
                                fileModel.uploadFailed(true);
                            }
                        },

                        // Called during upload progress, first parameter is decimal value from 0 to 100.
                        onProgress: function (progress) {                            
                            fileModel.uploadProgress(parseInt(progress, 10));
                        },
                        
                        onError: function () {                            
                            fileModel.uploadFailed(true);
                            uploadsModel.activeUploads.remove(fileModel);
                        }
                    });
                }
            });

            ko.applyBindings(uploadsModel, context);
        }
    }
        
    function trimTrailingZeros(number) {
        return number.toFixed(1).replace(/\.0+$/, '');
    }

    function formatFileSize(sizeInBytes) {
        var kiloByte = 1024,
            megaByte = Math.pow(kiloByte, 2),
            gigaByte = Math.pow(kiloByte, 3);

        if (sizeInBytes < kiloByte) {
            return sizeInBytes + ' B';
        }

        if (sizeInBytes < megaByte) {
            return trimTrailingZeros(sizeInBytes / kiloByte) + ' KB';
        }

        if (sizeInBytes < gigaByte) {
            return trimTrailingZeros(sizeInBytes / megaByte) + ' MB';
        }

        return trimTrailingZeros(sizeInBytes / gigaByte) + ' GB';
    }
        
    /**
    * Initializes page module.
    */
    mediaUpload.init = function () {        
    };

    /**
    * Register initialization
    */
    bcms.registerInit(mediaUpload.init);
        
    return mediaUpload;
});

angular.module('VirtoCommerce.Punchout')
    .controller('VirtoCommerce.Punchout.integrationDetailController', [
        '$scope',
        'platformWebApp.bladeNavigationService',
        'platformWebApp.dialogService',
        'platformWebApp.validators',
        'platformWebApp.clipboardService',
        'VirtoCommerce.Punchout.Integrations',
        function ($scope, bladeNavigationService, dialogService, validators, clipboardService, integrations) {
            var blade = $scope.blade;
            blade.headIcon = 'fas fa-key';
            blade.updatePermission = 'punchout:update';

            blade.refresh = function () {
                blade.isLoading = true;

                if (blade.isNew) {
                    integrations.getNew({}, initialize, onError);
                } else {
                    integrations.get({ id: blade.currentEntityId }, initialize, onError);
                }
            };

            function initialize(data) {
                data.allowedReturnUrls = data.allowedReturnUrls || [];

                blade.origEntity = data;
                blade.currentEntity = angular.copy(data);
                blade.isLoading = false;
            }

            function onError(error) {
                blade.isLoading = false;
                bladeNavigationService.setError('Error ' + error.status, blade);
            }

            function isDirty() {
                return !angular.equals(blade.currentEntity, blade.origEntity) && blade.hasUpdatePermission();
            }

            function isFormValid() {
                return !$scope.formScope || $scope.formScope.$valid;
            }

            function canSave() {
                return (blade.isNew || isDirty()) && isFormValid();
            }

            $scope.isValid = canSave;

            blade.onClose = function (closeCallback) {
                bladeNavigationService.showConfirmationIfNeeded(isDirty(), canSave(), blade, $scope.saveChanges, closeCallback,
                    'punchout.dialogs.integration-save.title', 'punchout.dialogs.integration-save.message');
            };

            $scope.setForm = function (form) {
                $scope.formScope = form;
            };

            $scope.cancelChanges = function () {
                $scope.bladeClose();
            };

            $scope.saveChanges = function () {
                blade.isLoading = true;

                var action = blade.isNew ? integrations.save : integrations.update;

                action({}, blade.currentEntity, function () {
                    blade.isLoading = false;
                    angular.copy(blade.currentEntity, blade.origEntity);
                    blade.parentBlade.refresh();
                    $scope.bladeClose();
                }, onError);
            };

            function deleteEntry() {
                var dialog = {
                    id: 'confirmDelete',
                    title: 'punchout.dialogs.integration-delete.title',
                    message: 'punchout.dialogs.integration-delete.message',
                    messageValues: { name: blade.currentEntity.name || blade.currentEntity.senderIdentity },
                    callback: function (remove) {
                        if (remove) {
                            blade.isLoading = true;
                            integrations.delete({ ids: [blade.currentEntity.id] }, function () {
                                blade.isLoading = false;
                                blade.parentBlade.refresh();
                                $scope.bladeClose();
                            }, onError);
                        }
                    }
                };

                dialogService.showDeleteConfirmationDialog(dialog);
            }

            blade.toolbarCommands = [
                {
                    name: 'platform.commands.refresh', icon: 'fa fa-refresh',
                    executeMethod: blade.refresh,
                    canExecuteMethod: function () {
                        return !blade.isNew;
                    }
                },
                {
                    name: 'platform.commands.reset', icon: 'fa fa-undo',
                    executeMethod: function () {
                        angular.copy(blade.origEntity, blade.currentEntity);
                    },
                    canExecuteMethod: isDirty,
                    permission: blade.updatePermission
                },
                {
                    name: 'platform.commands.delete', icon: 'fas fa-trash-alt',
                    executeMethod: deleteEntry,
                    canExecuteMethod: function () {
                        return !blade.isNew;
                    },
                    permission: 'punchout:delete'
                }
            ];

            $scope.copyToClipboard = function (elementId) {
                var element = document.getElementById(elementId);
                clipboardService.copyText(element.value || element.innerText);
            };

            $scope.editAllowedReturnUrls = function () {
                var newBlade = {
                    id: 'punchout-edit-allowedReturnUrls',
                    updatePermission: blade.updatePermission,
                    data: blade.currentEntity.allowedReturnUrls,
                    validator: validators.uriWithoutQuery,
                    headIcon: 'far fa-plus-square',
                    title: 'punchout.blades.integration-detail.blades.edit-allowedReturnUrls.title',
                    subtitle: 'punchout.blades.integration-detail.blades.edit-allowedReturnUrls.subtitle',
                    controller: 'platformWebApp.editArrayController',
                    template: '$(Platform)/Scripts/common/blades/edit-array.tpl.html',
                    onChangesConfirmedFn: function (values) {
                        blade.currentEntity.allowedReturnUrls = angular.copy(values);
                    }
                };

                bladeNavigationService.showBlade(newBlade, blade);
            };

            blade.refresh();
        }]);

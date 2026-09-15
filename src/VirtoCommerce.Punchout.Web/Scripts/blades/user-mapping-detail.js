angular.module('VirtoCommerce.Punchout')
    .controller('VirtoCommerce.Punchout.userMappingDetailController', [
        '$scope',
        'platformWebApp.bladeNavigationService',
        'platformWebApp.dialogService',
        'platformWebApp.metaFormsService',
        'VirtoCommerce.Punchout.UserMappings',
        function ($scope, bladeNavigationService, dialogService, metaFormsService, userMappings) {
            var blade = $scope.blade;
            blade.headIcon = 'fas fa-id-badge';
            blade.updatePermission = 'punchout:update';
            blade.metaFields = metaFormsService.getMetaFields("punchoutUserMappingDetail");
            $scope.securityAccounts = (blade.member && blade.member.securityAccounts) || [];

            blade.refresh = function () {
                blade.isLoading = true;

                // one contact has one mapping
                userMappings.search({ memberIds: [blade.member.id], take: 1 }, function (searchResult) {
                    if (searchResult.results.length) {
                        initialize(searchResult.results[0]);
                    } else {
                        userMappings.getNew({}, function (data) {
                            data.memberId = blade.member.id;

                            if ($scope.securityAccounts.length) {
                                setUser(data, $scope.securityAccounts[0].id);
                            }

                            initialize(data);
                        }, onError);
                    }
                }, onError);
            };

            function initialize(data) {
                blade.isNew = !data.id;
                blade.origEntity = data;
                blade.currentEntity = angular.copy(data);
                blade.isLoading = false;
            }

            function setUser(mapping, userId) {
                var account = _.find($scope.securityAccounts, function (x) {
                    return x.id === userId;
                });

                mapping.userId = userId;
                mapping.userName = account ? account.userName : null;
            }

            $scope.onUserChanged = function () {
                setUser(blade.currentEntity, blade.currentEntity.userId);
            };

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
                return isDirty() && isFormValid() && $scope.securityAccounts.length;
            }

            $scope.isValid = canSave;

            $scope.setForm = function (form) {
                $scope.formScope = form;
            };

            blade.onClose = function (closeCallback) {
                bladeNavigationService.showConfirmationIfNeeded(isDirty(), canSave(), blade, $scope.saveChanges, closeCallback,
                    'punchout.dialogs.user-mapping-save.title', 'punchout.dialogs.user-mapping-save.message');
            };

            $scope.cancelChanges = function () {
                $scope.bladeClose();
            };

            $scope.saveChanges = function () {
                blade.isLoading = true;

                var action = blade.isNew ? userMappings.save : userMappings.update;

                action({}, blade.currentEntity, function (data) {
                    initialize(data);

                    if (blade.onChangesConfirmedFn) {
                        blade.onChangesConfirmedFn();
                    }

                    $scope.bladeClose();
                }, onError);
            };

            function deleteEntry() {
                var dialog = {
                    id: 'confirmDeleteUserMapping',
                    title: 'punchout.dialogs.user-mapping-delete.title',
                    message: 'punchout.dialogs.user-mapping-delete.message',
                    messageValues: {
                        externalId:
                            blade.origEntity.externalId
                    },
                    callback: function (remove) {
                        if (remove) {
                            blade.isLoading = true;

                            userMappings.delete({ ids: [blade.origEntity.id] }, function () {
                                blade.isLoading = false;

                                if (blade.onChangesConfirmedFn) {
                                    blade.onChangesConfirmedFn();
                                }

                                $scope.bladeClose();
                            }, onError);
                        }
                    }
                };

                dialogService.showDeleteConfirmationDialog(dialog);
            }

            blade.toolbarCommands = [
                {
                    name: "platform.commands.save", icon: 'fas fa-save',
                    executeMethod: $scope.saveChanges,
                    canExecuteMethod: canSave,
                    permission: blade.updatePermission
                },
                {
                    name: 'platform.commands.refresh', icon: 'fa fa-refresh',
                    executeMethod: blade.refresh,
                    canExecuteMethod: function () {
                        return true;
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

            blade.refresh();
        }]);

angular.module('VirtoCommerce.Punchout')
    .controller('VirtoCommerce.Punchout.organizationIntegrationsController', [
        '$scope',
        'platformWebApp.bladeNavigationService',
        'VirtoCommerce.Punchout.Integrations',
        'VirtoCommerce.Punchout.OrganizationIntegrations',
        function ($scope, bladeNavigationService, integrations, organizationIntegrations) {
            var blade = $scope.blade;
            blade.headIcon = 'fas fa-key';
            blade.updatePermission = 'punchout:update';

            $scope.filter = {};

            blade.refresh = function () {
                blade.isLoading = true;

                integrations.search({ take: 1000 }, function (searchResult) {
                    organizationIntegrations.get({ organizationId: blade.organizationId }, function (assignedIds) {
                        blade.currentEntities = searchResult.results;
                        blade.totalCount = searchResult.totalCount;
                        blade.currentEntities.forEach(function (x) {
                            x.$selected = assignedIds.indexOf(x.id) >= 0;
                        });
                        blade.origSelectedIds = getSelectedIds();
                        blade.isLoading = false;
                    }, onError);
                }, onError);
            };

            function onError(error) {
                blade.isLoading = false;
                bladeNavigationService.setError('Error ' + error.status, blade);
            }

            function getSelectedIds() {
                return _.chain(blade.currentEntities)
                    .filter(function (x) { return x.$selected; })
                    .map(function (x) { return x.id; })
                    .value()
                    .sort();
            }

            function isDirty() {
                return !!blade.origSelectedIds &&
                    !angular.equals(getSelectedIds(), blade.origSelectedIds) &&
                    blade.hasUpdatePermission();
            }

            $scope.isValid = isDirty;

            blade.onClose = function (closeCallback) {
                bladeNavigationService.showConfirmationIfNeeded(isDirty(), true, blade, $scope.saveChanges, closeCallback,
                    'punchout.dialogs.organization-integrations-save.title', 'punchout.dialogs.organization-integrations-save.message');
            };

            $scope.cancelChanges = function () {
                $scope.bladeClose();
            };

            $scope.saveChanges = function () {
                blade.isLoading = true;

                var selectedIds = getSelectedIds();

                organizationIntegrations.update({ organizationId: blade.organizationId }, selectedIds, function () {
                    blade.origSelectedIds = selectedIds;
                    blade.isLoading = false;

                    if (blade.onChangesConfirmedFn) {
                        blade.onChangesConfirmedFn();
                    }

                    $scope.bladeClose();
                }, onError);
            };

            $scope.toggleAll = function () {
                blade.currentEntities.forEach(function (x) {
                    x.$selected = blade.allSelected;
                });
            };

            blade.toolbarCommands = [
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
                        blade.currentEntities.forEach(function (x) {
                            x.$selected = blade.origSelectedIds.indexOf(x.id) >= 0;
                        });
                        blade.allSelected = false;
                    },
                    canExecuteMethod: isDirty,
                    permission: blade.updatePermission
                }
            ];

            blade.refresh();
        }]);

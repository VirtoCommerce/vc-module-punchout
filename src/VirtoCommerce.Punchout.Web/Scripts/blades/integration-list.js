angular.module('VirtoCommerce.Punchout')
    .controller('VirtoCommerce.Punchout.integrationListController', [
        '$scope',
        'platformWebApp.bladeNavigationService',
        'platformWebApp.dialogService',
        'platformWebApp.bladeUtils',
        'platformWebApp.uiGridHelper',
        'VirtoCommerce.Punchout.Integrations',
        function ($scope, bladeNavigationService, dialogService, bladeUtils, uiGridHelper, integrations) {
            var blade = $scope.blade;
            blade.title = 'punchout.blades.integration-list.title';
            blade.headIcon = 'fas fa-key';
            blade.updatePermission = 'punchout:update';

            blade.refresh = function () {
                blade.isLoading = true;

                var criteria = {
                    sort: uiGridHelper.getSortExpression($scope),
                    skip: ($scope.pageSettings.currentPage - 1) * $scope.pageSettings.itemsPerPageCount,
                    take: $scope.pageSettings.itemsPerPageCount
                };

                integrations.search(criteria, function (data) {
                    blade.isLoading = false;
                    blade.currentEntities = data.results;
                    $scope.pageSettings.totalItems = data.totalCount;
                }, function (error) {
                    blade.isLoading = false;
                    bladeNavigationService.setError('Error ' + error.status, blade);
                });
            };

            blade.selectNode = function (node) {
                $scope.selectedNodeId = node.id;

                openDetailsBlade({
                    subtitle: 'punchout.blades.integration-detail.subtitle',
                    currentEntityId: node.id
                });
            };

            function openDetailsBlade(node) {
                var newBlade = {
                    id: 'punchout-integration-detail',
                    title: blade.title,
                    controller: 'VirtoCommerce.Punchout.integrationDetailController',
                    template: 'Modules/$(VirtoCommerce.Punchout)/Scripts/blades/integration-detail.tpl.html'
                };

                angular.extend(newBlade, node);
                bladeNavigationService.showBlade(newBlade, blade);
            }

            $scope.deleteList = function (selection) {
                var dialog = {
                    id: 'confirmDeleteItem',
                    title: 'punchout.dialogs.integration-delete.title',
                    items: [
                        { key: 'punchout.dialogs.integration-delete.integration', count: selection.length }
                    ],
                    callback: function (remove) {
                        if (remove) {
                            bladeNavigationService.closeChildrenBlades(blade, function () {
                                blade.isLoading = true;
                                var ids = _.map(selection, function (x) { return x.id; });
                                integrations.delete({ ids: ids }, blade.refresh, function (error) {
                                    blade.isLoading = false;
                                    bladeNavigationService.setError('Error ' + error.status, blade);
                                });
                            });
                        }
                    }
                };

                dialogService.showDeleteConfirmationDialog(dialog);
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
                    name: 'platform.commands.add', icon: 'fas fa-plus',
                    executeMethod: function () {
                        bladeNavigationService.closeChildrenBlades(blade, function () {
                            $scope.selectedNodeId = undefined;

                            openDetailsBlade({
                                subtitle: 'punchout.blades.integration-detail.subtitle-new',
                                isNew: true
                            });
                        });
                    },
                    canExecuteMethod: function () {
                        return true;
                    },
                    permission: 'punchout:create'
                },
                {
                    name: 'platform.commands.delete', icon: 'fas fa-trash-alt',
                    executeMethod: function () { $scope.deleteList($scope.gridApi.selection.getSelectedRows()); },
                    canExecuteMethod: function () {
                        return $scope.gridApi && _.any($scope.gridApi.selection.getSelectedRows());
                    },
                    permission: 'punchout:delete'
                }
            ];

            // ui-grid
            $scope.setGridOptions = function (gridOptions) {
                uiGridHelper.initialize($scope, gridOptions, function (gridApi) {
                    $scope.gridApi = gridApi;
                    uiGridHelper.bindRefreshOnSortChanged($scope);
                });
                bladeUtils.initializePagination($scope);
            };

            // No need to call blade.refresh() here: 'pageSettings.currentPage' is watched by the pagination helper.
        }]);

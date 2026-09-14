angular.module('VirtoCommerce.Punchout')
    .controller('VirtoCommerce.Punchout.organizationPunchoutIntegrationWidgetController',
        ['$scope', 'platformWebApp.bladeNavigationService', 'VirtoCommerce.Punchout.OrganizationIntegrations',
            function ($scope, bladeNavigationService, organizationIntegrations) {
                var blade = $scope.widget.blade;

                $scope.integrationCount = 0;

                function refresh() {
                    if (!blade.currentEntity || !blade.currentEntity.id) {
                        return;
                    }

                    $scope.widget.isLoading = true;

                    organizationIntegrations.get({ organizationId: blade.currentEntity.id }, function (integrationIds) {
                        $scope.integrationCount = integrationIds.length;
                        $scope.widget.isLoading = false;
                    }, function () {
                        $scope.widget.isLoading = false;
                    });
                }

                $scope.openBlade = function () {
                    if (!blade.currentEntity || !blade.currentEntity.id) {
                        return;
                    }

                    var newBlade = {
                        id: 'organizationPunchoutIntegrationBlade',
                        title: 'punchout.blades.organization-integrations.title',
                        subtitle: blade.currentEntity.name,
                        organizationId: blade.currentEntity.id,
                        controller: 'VirtoCommerce.Punchout.organizationIntegrationsController',
                        template: 'Modules/$(VirtoCommerce.Punchout)/Scripts/blades/organization-integrations.tpl.html',
                        onChangesConfirmedFn: refresh
                    };

                    bladeNavigationService.showBlade(newBlade, blade);
                };

                // The organization is loaded asynchronously, so the widget waits for it instead of reading it once.
                $scope.$watch('widget.blade.currentEntity.id', function (id) {
                    if (id) {
                        refresh();
                    }
                });
            }]);

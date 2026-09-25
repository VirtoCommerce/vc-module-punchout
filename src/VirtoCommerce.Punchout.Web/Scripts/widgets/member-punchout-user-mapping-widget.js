angular.module('VirtoCommerce.Punchout')
    .controller('VirtoCommerce.Punchout.memberPunchoutUserMappingWidgetController',
        ['$scope', 'platformWebApp.bladeNavigationService', 'VirtoCommerce.Punchout.UserMappings',
            function ($scope, bladeNavigationService, userMappings) {
                var blade = $scope.widget.blade;

                $scope.mapping = null;

                function refresh() {
                    if (!blade.currentEntity || !blade.currentEntity.id) {
                        return;
                    }

                    $scope.widget.isLoading = true;

                    userMappings.search({ memberIds: [blade.currentEntity.id], take: 1 }, function (searchResult) {
                        $scope.mapping = searchResult.results.length ? searchResult.results[0] : null;
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
                        id: 'memberPunchoutUserMappingBlade',
                        title: 'punchout.blades.user-mapping-detail.title',
                        subtitle: blade.currentEntity.name,
                        member: blade.currentEntity,
                        controller: 'VirtoCommerce.Punchout.userMappingDetailController',
                        template: 'Modules/$(VirtoCommerce.Punchout)/Scripts/blades/user-mapping-detail.tpl.html',
                        onChangesConfirmedFn: refresh
                    };

                    bladeNavigationService.showBlade(newBlade, blade);
                };

                // The member is loaded asynchronously, so the widget waits for it instead of reading it once.
                $scope.$watch('widget.blade.currentEntity.id', function (id) {
                    if (id) {
                        refresh();
                    }
                });
            }]);

// Call this to register your module to main application
var moduleName = 'VirtoCommerce.Punchout';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .config(['$stateProvider',
        function ($stateProvider) {
            $stateProvider
                .state('workspace.PunchoutState', {
                    url: '/punchout',
                    templateUrl: '$(Platform)/Scripts/common/templates/home.tpl.html',
                    controller: [
                        'platformWebApp.bladeNavigationService',
                        function (bladeNavigationService) {
                            var newBlade = {
                                id: 'punchout-integration-list',
                                controller: 'VirtoCommerce.Punchout.integrationListController',
                                template: 'Modules/$(VirtoCommerce.Punchout)/Scripts/blades/integration-list.tpl.html',
                                isClosingDisabled: true,
                            };
                            bladeNavigationService.showBlade(newBlade);
                        }
                    ]
                });
        }
    ])
    .run(['platformWebApp.mainMenuService', '$state', 'platformWebApp.widgetService',
        function (mainMenuService, $state, widgetService) {
            //Register module in main menu
            var menuItem = {
                path: 'browse/punchout',
                icon: 'fa fa-cube',
                title: 'Punchout',
                priority: 100,
                action: function () { $state.go('workspace.PunchoutState'); },
                permission: 'punchout:access',
            };
            mainMenuService.addMenuItem(menuItem);


            // widgets
            var organizationPunchoutIntegrationWidget = {
                controller: 'VirtoCommerce.Punchout.organizationPunchoutIntegrationWidgetController',
                template: 'Modules/$(VirtoCommerce.Punchout)/Scripts/widgets/organization-punchout-integration-widget.html',
                size: [2, 1],
                permission: 'punchout:read',
                isVisible: function (blade) {
                    return !blade.isNew;
                }
            };

            widgetService.registerWidget(organizationPunchoutIntegrationWidget, 'organizationDetail2');
        }
    ]);

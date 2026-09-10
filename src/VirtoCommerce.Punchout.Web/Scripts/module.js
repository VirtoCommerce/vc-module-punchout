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
                                id: 'blade1',
                                controller: 'VirtoCommerce.Punchout.helloWorldController',
                                template: 'Modules/$(VirtoCommerce.Punchout)/Scripts/blades/hello-world.html',
                                isClosingDisabled: true,
                            };
                            bladeNavigationService.showBlade(newBlade);
                        }
                    ]
                });
        }
    ])
    .run(['platformWebApp.mainMenuService', '$state',
        function (mainMenuService, $state) {
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
        }
    ]);

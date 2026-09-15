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
    .run(['platformWebApp.mainMenuService', '$state', 'platformWebApp.widgetService', 'platformWebApp.metaFormsService',
        function (mainMenuService, $state, widgetService, metaFormsService) {
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
            var memberPunchoutUserMappingWidget = {
                controller: 'VirtoCommerce.Punchout.memberPunchoutUserMappingWidgetController',
                template: 'Modules/$(VirtoCommerce.Punchout)/Scripts/widgets/member-punchout-user-mapping-widget.html',
                size: [2, 1],
                permission: 'punchout:read',
                isVisible: function (blade) {
                    return !blade.isNew;
                }
            };

            widgetService.registerWidget(memberPunchoutUserMappingWidget, 'customerDetail2');

            // metaforms
            metaFormsService.registerMetaFields('punchoutUserMappingDetail', [
                {
                    name: 'isActive',
                    title: 'punchout.blades.user-mapping-detail.labels.isActive',
                    valueType: "Boolean",
                    colSpan: 6
                },
                {
                    name: 'externalId',
                    title: 'punchout.blades.user-mapping-detail.labels.externalId',
                    placeholder: 'punchout.blades.user-mapping-detail.placeholders.externalId',
                    valueType: "ShortText",
                    isRequired: true,
                    colSpan: 6
                }
            ]);
        }
    ]);

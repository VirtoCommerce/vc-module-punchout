// Call this to register your module to main application
var moduleName = 'VirtoCommerce.Punchout';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .run(['platformWebApp.widgetService', 'platformWebApp.metaFormsService', 'platformWebApp.authService',
        function (widgetService, metaFormsService, authService) {
            // widgets
            var memberPunchoutUserMappingWidget = {
                controller: 'VirtoCommerce.Punchout.memberPunchoutUserMappingWidgetController',
                template: 'Modules/$(VirtoCommerce.Punchout)/Scripts/widgets/member-punchout-user-mapping-widget.html',
                size: [2, 1],
                isVisible: function (blade) {
                    // widget.permission and isVisible() don't work together
                    return !blade.isNew && authService.checkPermission('punchout:read')
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

angular.module('VirtoCommerce.Punchout')
    .factory('VirtoCommerce.Punchout.Integrations', ['$resource', function ($resource) {
        return $resource('api/punchout-integrations', {}, {
            get: { method: 'GET', url: 'api/punchout-integrations/:id' },
            getNew: { method: 'GET', url: 'api/punchout-integrations/new' },
            search: { method: 'POST', url: 'api/punchout-integrations/search' },
            save: { method: 'POST' },
            update: { method: 'PUT' },
            delete: { method: 'DELETE' },
        });
    }])
    .factory('VirtoCommerce.Punchout.OrganizationIntegrations', ['$resource', function ($resource) {
        return $resource('api/punchout/organizations/:organizationId/integrations', { organizationId: '@organizationId' }, {
            get: { method: 'GET', isArray: true },
            update: { method: 'PUT' },
        });
    }]);

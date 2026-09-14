angular.module('VirtoCommerce.Punchout')
    .factory('VirtoCommerce.Punchout.Integrations', ['$resource', function ($resource) {
        return $resource('api/punchout-integrations', {}, {
            get: { method: 'GET', url: 'api/punchout-integrations/:id' },
            getNew: { url: 'api/punchout-integrations/new' },
            search: { url: 'api/punchout-integrations/search', method: 'POST' },
            save: { method: 'POST' },
            update: { method: 'PUT' },
            delete: { method: 'DELETE' },
        });
    }]);

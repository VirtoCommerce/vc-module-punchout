angular.module('VirtoCommerce.Punchout')
    .factory('VirtoCommerce.Punchout.UserMappings', ['$resource', function ($resource) {
        return $resource('api/punchout-user-mappings', {}, {
            get: { method: 'GET', url: 'api/punchout-user-mappings/:id' },
            getNew: { method: 'GET', url: 'api/punchout-user-mappings/new' },
            search: { method: 'POST', url: 'api/punchout-user-mappings/search' },
            save: { method: 'POST' },
            update: { method: 'PUT' },
            delete: { method: 'DELETE' },
        });
    }]);

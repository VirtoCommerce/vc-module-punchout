angular.module('VirtoCommerce.Punchout')
    .factory('VirtoCommerce.Punchout.webApi', ['$resource', function ($resource) {
        return $resource('api/punchout');
    }]);

angular.module('VirtoCommerce.Punchout')
    .controller('VirtoCommerce.Punchout.helloWorldController', ['$scope', 'VirtoCommerce.Punchout.webApi', function ($scope, api) {
        var blade = $scope.blade;
        blade.title = 'Punchout';

        blade.refresh = function () {
            api.get(function (data) {
                blade.title = 'punchout.blades.hello-world.title';
                blade.data = data.result;
                blade.isLoading = false;
            });
        };

        blade.refresh();
    }]);

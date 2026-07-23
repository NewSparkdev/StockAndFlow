using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class BomService
    {
        private readonly IDataService _dataService;

        public BomService(IDataService dataService) => _dataService = dataService;

        public Task<List<BomComponent>> GetComponentsForItemAsync(Guid parentItemId) =>
            _dataService.GetWhereAsync<BomComponent>(c => c.ParentItemId == parentItemId);

        public async Task SaveComponentsForItemAsync(Guid parentItemId, IEnumerable<BomComponent> components)
        {
            var existing = await GetComponentsForItemAsync(parentItemId);
            foreach (var e in existing)
                await _dataService.DeleteAsync<BomComponent>(e.Id);
            foreach (var c in components)
            {
                c.ParentItemId = parentItemId;
                await _dataService.SaveAsync(c);
            }
        }
    }
}

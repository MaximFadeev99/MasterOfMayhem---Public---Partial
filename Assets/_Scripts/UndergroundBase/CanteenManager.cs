using System;
using System.Collections.Generic;
using UnityEngine;
using MasterOfMayhem.Provision;
using MasterOfMayhem.Utilities;
using System.Linq;
using MasterOfMayhem.PlaceableObjects.Tables;
using MasterOfMayhem.Humanoids.AbstractHumanoid;

namespace MasterOfMayhem.Base
{
    [Serializable]
    public class CanteenManager
    {
        [SerializeField] private List<Food> _foodPrefabs;
        [SerializeField] private Transform _foodContainer;

        private readonly List<Table> _allTables = new();
        private readonly Dictionary<Humanoid, Chair> _minionChairDictionary = new();

        private List<MonoBehaviourPool<Food>> _foodPools;

        public void Initialize() 
        {
            _foodPools = new();

            foreach (Food foodPrefab in _foodPrefabs) 
            {
                MonoBehaviourPool<Food> newPool = new(foodPrefab, _foodContainer, 10);
                _foodPools.Add(newPool);
            }
        }

        public Food OrderFoodProduct(Type type) 
        {
            if (_foodPools == null) 
            {
                Debug.LogAssertion($"{nameof(CanteenManager)} has not been initialized. Call Initialize method first " +
                    $"before requesting food");

                return null;
            }

            MonoBehaviourPool<Food> targetPool = _foodPools.FirstOrDefault(pool => pool.AllElements[0].GetType() == type);

            if (targetPool == null) 
            {
                Debug.LogAssertion($"{nameof(CanteenManager)} can not spawn an object of type {type}. Add a corresponding prefab" +
                    $"to {nameof(CanteenManager)} first");
            }
            
            Food orderedFoodProduct = targetPool.GetIdleElement();
            orderedFoodProduct.GameObject.SetActive(true);

            return orderedFoodProduct;
        }

        public IReadOnlyList<Type> GetAvailableFoodTypes() 
        {
            if (_foodPrefabs.Count == 0) 
            {
                Debug.LogAssertion($"No food has been added to {nameof(CanteenManager)}");
                return null;
            }
                        
            return _foodPrefabs.Select(prefab => prefab.GetType()).ToList();
        }

        public void InformOfNewTables(IReadOnlyList<Table> newlyConstructedTables)
        {
            foreach (Table table in newlyConstructedTables)
            {
                _allTables.Add(table);
                table.Demolished += OnTableDemolished;
            }
        }

        public Chair RequestDiningPlace(Humanoid applicant, out Table assignedTable) 
        {
            if (applicant == null || _allTables.Count == 0 ||
                 _allTables.All(table => table.IsOccupied || table.IsConstructed == false || table.IsSabotaged.Value))
            {
                assignedTable = null;
                return null;
            }

            assignedTable = _allTables
                .Where(table => table.IsConstructed && table.IsSabotaged.Value == false)
                .OrderByDescending(table => table.VacantChairCount)
                .First();
            Chair vacantChair = assignedTable.GetRandomVacantChair();
            vacantChair.ToggleOccupiedProperty(true);
            _minionChairDictionary.Add(applicant, vacantChair);

            return vacantChair;       
        }

        public void NotifyOfMealEnd(Humanoid humanoidFinishedEating) 
        {
            if (_minionChairDictionary.ContainsKey(humanoidFinishedEating) == false)
                return;

            _minionChairDictionary[humanoidFinishedEating].ToggleOccupiedProperty(false);
            _minionChairDictionary.Remove(humanoidFinishedEating);
        }


        private void OnTableDemolished(Table demolishedTable)
        {
            demolishedTable.Demolished -= OnTableDemolished;
            _allTables.Remove(demolishedTable);
            IReadOnlyList<Chair> demolishedChairs = demolishedTable.AllChairs;
            List<Humanoid> occupyingMinions = _minionChairDictionary
                .Where(kvPair => kvPair.Value == demolishedChairs.Any())
                .Select(kvPair => kvPair.Key)
                .ToList();

            foreach (Humanoid minion in occupyingMinions)
                _minionChairDictionary.Remove(minion);
        }
    }
}
using MasterOfMayhem.Construction;
using MasterOfMayhem.PlaceableObjects.Beds;
using MasterOfMayhem.PlaceableObjects.Buildings;
using MasterOfMayhem.PlaceableObjects.Tables;
using MasterOfMayhem.Tasks;
using MasterOfMayhem.UCA.Blocks;
using MasterOfMayhem.UCA.Blocks.BlockParts;
using MasterOfMayhem.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterOfMayhem.Base
{
    public class UndergroundBase : MonoBehaviour
    {
        [SerializeField] private BuildingSystem _buildingSystem;
        [SerializeField] private StateIndicationSystem _stateIndicationSystem;
        [SerializeField] private LayerMask _terrainMask;
        [SerializeField] private LayerMask _floorMask;
        [Tooltip("This value will be used to randomize how far from an UndergroundEntrance " +
            "a minion can walk away when they are idle")]
        [SerializeField] private float _entranceStrayDistance = 20f;

        private readonly List<UndergroundEntrance> _constructedEntrances = new();

        private List<Floor> _baseFloors = new();

        [field: SerializeField] public CanteenManager CanteenManager { get; private set; }
        [field: SerializeField] public ArmoryManager ArmoryManager { get; private set; }
        [field: SerializeField] public SecurityManager SecurityManager { get; private set; }
        public RandomPointGetter RandomPointGetter { get; private set; }
        public HumanResourceManager HumanResourceManager { get; private set;}
        public WarehouseManager WarehouseManager { get; private set; }
        public TaskManager TaskManager { get; private set; }
        public WorkingPlaceManager WorkingPlaceManager { get; private set; }
        public PlacedObjectManager PlacedObjectManager { get; private set; }
        
        public void Initialize()
        {
            int terrainLayer = Mathf.RoundToInt(Mathf.Log(_terrainMask.value, 2));
            int floorLayer = Mathf.RoundToInt(Mathf.Log(_floorMask.value, 2));

            RandomPointGetter = new(terrainLayer, floorLayer, _entranceStrayDistance);
            HumanResourceManager = new();
            TaskManager = new(HumanResourceManager, TaskPriorities.Medium);
            WarehouseManager = new();
            WorkingPlaceManager = new(TaskManager);
            PlacedObjectManager = new(_stateIndicationSystem);
            SecurityManager.Intialize(TaskManager);
            CanteenManager.Initialize();
        }

        private void OnEnable()
        {
            _buildingSystem.AccessibleFloorsChanged += ReregisterBaseFloors;
            _buildingSystem.UndergroundEntrancesAdded += OnUndergroundEntrancesAdded;
            _buildingSystem.SpawningBuildingsAdded += OnSpawningBuildingsAdded;
            _buildingSystem.BedsAdded += OnBedsAdded;
            _buildingSystem.DiningTablesAdded += OnDiningTablesAdded;
            _buildingSystem.WorkingPlacesAdded += OnWorkingPlacesAdded;
        }

        private void OnDisable()
        {
            _buildingSystem.AccessibleFloorsChanged -= ReregisterBaseFloors;
            _buildingSystem.UndergroundEntrancesAdded -= OnUndergroundEntrancesAdded;
            _buildingSystem.SpawningBuildingsAdded -= OnSpawningBuildingsAdded;
            _buildingSystem.BedsAdded -= OnBedsAdded;
            _buildingSystem.DiningTablesAdded -= OnDiningTablesAdded;
            _buildingSystem.WorkingPlacesAdded -= OnWorkingPlacesAdded;
        }

        private void OnDestroy()
        {
            SecurityManager.OnDestroy();
            HumanResourceManager.OnDestroy();
            TaskManager.OnDestroy();
            WorkingPlaceManager.OnDestroy();
        }

        public bool CanEntranceBeDestroyed() 
        {
            return _constructedEntrances.Count > 1;       
        }

        private void ReregisterBaseFloors(IReadOnlyList<Floor> availableFloors) 
        {
            _baseFloors.Clear();
            _baseFloors = availableFloors.ToList();   
            RandomPointGetter.UpdateFloors(_baseFloors);
        }

        private void OnUndergroundEntrancesAdded
            (IReadOnlyList<UndergroundEntrance> newlyConstructedEntrances) 
        {
            if (newlyConstructedEntrances.Count == 0)
                return;

            for (int i = 0; i < newlyConstructedEntrances.Count; i++) 
            {
                _constructedEntrances.Add(newlyConstructedEntrances[i]);
                newlyConstructedEntrances[i].Demolished += OnEntranceDemolished;
                newlyConstructedEntrances[i].CheckIfCanBeDestroyed += CanEntranceBeDestroyed;
            }

            RandomPointGetter.UpdateEntrances(_constructedEntrances);
            SecurityManager.UpdateEntrances(_constructedEntrances);
        }

        private void OnEntranceDemolished(UndergroundEntrance entrance) 
        {
            entrance.Demolished -= OnEntranceDemolished;
            entrance.CheckIfCanBeDestroyed -= CanEntranceBeDestroyed;
            _constructedEntrances.Remove(entrance);

            foreach (ClearableBlock block in entrance.InitiallyAccessibleArea) 
            {
                _baseFloors.Remove(block.Floor);
            }

            RandomPointGetter.UpdateEntrances(_constructedEntrances);
            SecurityManager.UpdateEntrances(_constructedEntrances);
        }

        private void OnSpawningBuildingsAdded(IReadOnlyList<ObjectSpawningBuilding> newlyConstructedSpawningBuildings)
        {
            WarehouseManager.InformOfNewSpawningBuildings(newlyConstructedSpawningBuildings);
        }

        private void OnBedsAdded(IReadOnlyList<Bed> newlyConstructedBeds) 
        {
            HumanResourceManager.InformOfNewBeds(newlyConstructedBeds);
        }

        private void OnDiningTablesAdded(IReadOnlyList<Table> newlyConstructedTables) 
        {
            CanteenManager.InformOfNewTables(newlyConstructedTables);
        }

        private void OnWorkingPlacesAdded (IReadOnlyList<WorkingPlace> newlyConstructedWorkingPlaces)
        {
            WorkingPlaceManager.InformOfNewWorkingPlaces(newlyConstructedWorkingPlaces);
        }
    }
}
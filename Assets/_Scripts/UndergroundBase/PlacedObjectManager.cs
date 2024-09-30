using MasterOfMayhem.Interfaces;
using MasterOfMayhem.PlaceableObjects;
using MasterOfMayhem.PlaceableObjects.Buildings;
using MasterOfMayhem.UI;
using System.Collections.Generic;

namespace MasterOfMayhem.Base
{
    public class PlacedObjectManager
    {
        private readonly List<PlaceableObject> _placedObjects = new();
        private readonly List<PlaceableObject> _constructedObjects = new();
        private readonly StateIndicationSystem _stateIndicationSystem;

        public IReadOnlyList<PlaceableObject> ConstructedObjects => _constructedObjects;

        public PlacedObjectManager(StateIndicationSystem stateIndicationSystem) 
        {
            _stateIndicationSystem = stateIndicationSystem;
        }

        public void RegisterPlacedObjects(params PlaceableObject[] placedObjects)
        {
            foreach (PlaceableObject placedObject in placedObjects)
            {
                _placedObjects.Add(placedObject);
                placedObject.Constructed += OnObjectConstructed;
                placedObject.Demolished += OnObjectDemolished;
                placedObject.IsSabotaged.Subscribe(OnObjectSabotagedValueChanged);

                if (placedObject is Building placedBuilding)
                    placedBuilding.WorkingPropertyChanged += OnBuildingWorkingPropertyChanged;
            }
        }

        private void OnObjectConstructed(PlaceableObject constructedObject)
        {
            _constructedObjects.Add(constructedObject);
            constructedObject.Constructed -= OnObjectConstructed;
        }

        private void OnObjectDemolished(PlaceableObject demolishedObject)
        {
            _placedObjects.Remove(demolishedObject);

            if (_constructedObjects.Contains(demolishedObject))
                _constructedObjects.Remove(demolishedObject);

            demolishedObject.Constructed -= OnObjectConstructed;
            demolishedObject.Demolished -= OnObjectDemolished;
            demolishedObject.IsSabotaged.Unsubscribe(OnObjectSabotagedValueChanged);

            if (demolishedObject is Building demolishedBuilding)
                demolishedBuilding.WorkingPropertyChanged -= OnBuildingWorkingPropertyChanged;
        }

        private void OnObjectSabotagedValueChanged(bool isSabotaged, ISabotagable sabotagedObject) 
        {
            if (sabotagedObject is IIndicatable indicatable == false)
                return;

            if (isSabotaged)
                _stateIndicationSystem.AddIndication(indicatable, StateTypes.BuildingSabotaged);
            else
                _stateIndicationSystem.RemoveIndication(indicatable);
        }

        private void OnBuildingWorkingPropertyChanged(Building building, bool isWorking)
        {
            if (building.IsSabotaged.Value)
                return;

            if (isWorking == false)
                _stateIndicationSystem.AddIndication(building, StateTypes.BuildingNotWorking);
            else
                _stateIndicationSystem.RemoveIndication(building);
        }
    }
}
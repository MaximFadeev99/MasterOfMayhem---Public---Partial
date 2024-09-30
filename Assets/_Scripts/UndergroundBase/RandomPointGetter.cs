using MasterOfMayhem.PlaceableObjects.Buildings;
using MasterOfMayhem.UCA.Blocks;
using MasterOfMayhem.UCA.Blocks.BlockParts;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace MasterOfMayhem.Base
{
    public class RandomPointGetter
    {
        private readonly List<UndergroundEntrance> _constructedEntrances = new();
        private readonly List<Floor> _availableFloors = new();
        private readonly int _terrainLayer;
        private readonly int _floorLayer;
        private readonly float _entranceStrayDistance;

        private IReadOnlyList<UndergroundEntrance> _placedEntrances = null;

        public RandomPointGetter(int terrainLayer, int floorLayer, float entranceStrayDistance)
        {
            _terrainLayer = terrainLayer;
            _floorLayer = floorLayer;
            _entranceStrayDistance = entranceStrayDistance;
        }

        public Vector3 GetRandomPoint()
        {
            if (_constructedEntrances.Count == 0)
                return Vector3.zero;

            int randomIndex = Random.Range(0, _availableFloors.Count + _constructedEntrances.Count);
            Vector3 randomPosition;

            if (randomIndex > _availableFloors.Count - 1)
            {
                randomIndex -= _availableFloors.Count;
                randomPosition = GetRandomPositionNearEntrance(randomIndex);
            }
            else
            {
                randomPosition = GetRandomPositionOnFloor(randomIndex);
            }

            return randomPosition;
        }

        public bool CheckPointValidity(Vector3 pointToCheck, out Vector3 suggestedValidPoint) 
        {
            bool isPointValid = true;
            int closestEntranceIndex = 0;

            if (NavMesh.SamplePosition(pointToCheck, out NavMeshHit hit, 5f, NavMesh.AllAreas) == false)
            {
                suggestedValidPoint = GetRandomPoint();
                return false;
            }
            else 
            {
                suggestedValidPoint = hit.position;         
            }

            if (_constructedEntrances.Count == 0)
                return true;

            for (int i = 0; i < _constructedEntrances.Count; i++)
            {
                if (_constructedEntrances[i].CheckIfPointInNonIdleZone(pointToCheck)) 
                {
                    isPointValid = false;
                    closestEntranceIndex = i;
                    break;
                }
            }

            if (isPointValid)
                return true;

            suggestedValidPoint = GetRandomPositionNearEntrance(closestEntranceIndex);

            return false;         
        }

        public void UpdateFloors(IReadOnlyList<Floor> newFloors) 
        {
            _availableFloors.AddRange(newFloors);
        }

        public void UpdateEntrances(IReadOnlyList<UndergroundEntrance> newEntrances)
        {
            if (_placedEntrances != null) 
            {
                foreach (UndergroundEntrance entrance in _placedEntrances)
                    entrance.Constructed -= OnEntranceConstructed;
            }

            _placedEntrances = newEntrances;

            foreach (UndergroundEntrance entrance in _placedEntrances)
                entrance.Constructed += OnEntranceConstructed;

            if (_placedEntrances.Count == 0) 
            {
                _availableFloors.Clear();
                return;
            }

            foreach (UndergroundEntrance entrance in _placedEntrances) 
            {
                foreach (ClearableBlock block in entrance.InitiallyAccessibleArea)
                    _availableFloors.Remove(block.Floor);            
            }
        }

        private Vector3 GetRandomPositionNearEntrance(int entranceIndex)
        {
            Vector3 randomPosition;
            bool isPositionSampled;
            int breakCount = 1;

            do
            {
                randomPosition = _constructedEntrances[entranceIndex].Transform.position;
                randomPosition.x += Random.Range(-_entranceStrayDistance, _entranceStrayDistance);
                randomPosition.z += Random.Range(-_entranceStrayDistance, _entranceStrayDistance);

                isPositionSampled = NavMesh.SamplePosition
                    (randomPosition, out NavMeshHit navMeshHit, 10f, _terrainLayer);

                if (isPositionSampled)
                    randomPosition = navMeshHit.position;

                breakCount++;

                if (breakCount > 100)
                {
                    Debug.LogAssertion("Random point generation inside Base failed on an Entrance");
                    return Vector3.zero;
                }
            }
            while (_constructedEntrances[entranceIndex].CheckIfPointInNonIdleZone(randomPosition) ||
            isPositionSampled == false);

            return randomPosition;
        }

        private Vector3 GetRandomPositionOnFloor(int startFloorIndex)
        {
            Vector3 randomPosition;
            bool isPositionSampled;
            bool isIndexChangeRequired = false;
            int breakCount = 1;

            do
            {
                if (isIndexChangeRequired)
                {
                    startFloorIndex = Random.Range(0, _availableFloors.Count);
                    isIndexChangeRequired = false;
                }

                randomPosition = _availableFloors[startFloorIndex].Transform.position;
                randomPosition.x += Random.Range(-_availableFloors[startFloorIndex].Renderer.bounds.extents.x,
                    _availableFloors[startFloorIndex].Renderer.bounds.extents.x);
                randomPosition.z += Random.Range(-_availableFloors[startFloorIndex].Renderer.bounds.extents.z,
                    _availableFloors[startFloorIndex].Renderer.bounds.extents.z);

                isPositionSampled = NavMesh.SamplePosition
                    (randomPosition, out NavMeshHit navMeshHit, 10f, _floorLayer);

                if (isPositionSampled)
                    randomPosition = navMeshHit.position;

                foreach (UndergroundEntrance entrance in _constructedEntrances)
                {
                    if (entrance.CheckIfPointInNonIdleZone(randomPosition))
                    {
                        isIndexChangeRequired = true;
                        break;
                    }
                }

                breakCount++;
                if (breakCount > 100)
                {
                    Debug.LogAssertion("Random point generation inside Base failed on Floors");
                    return Vector3.zero;
                }
            }
            while (isIndexChangeRequired || isPositionSampled == false);

            return randomPosition;
        }

        private void OnEntranceConstructed(UndergroundEntrance constructedEntrance) 
        {
            constructedEntrance.Constructed -= OnEntranceConstructed;
            _constructedEntrances.Add(constructedEntrance);
            _availableFloors.AddRange(constructedEntrance.InitiallyAccessibleArea.Select(block => block.Floor));
        }
    }
}
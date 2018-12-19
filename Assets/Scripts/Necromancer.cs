﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class Necromancer : Enemy
{
    EventFSM<BossActions> _fsm;

    [SerializeField] Barrier[] _barriers;

    [SerializeField] Skeleton[] _skeletons;
    Vector3[] _skeletonPositions;

    [SerializeField] EnergyBall _energyBallPrefab;
    [SerializeField] Transform _shootPoint;
    [SerializeField] int _minAmountOfShots, _maxAmountOfShots;
    [SerializeField] int _currentShots;
    [SerializeField] int _targetShots;

    [SerializeField] Transform[] _waypoints;
    Vector3 _currentWaypoint;

    [SerializeField] float _rangeToBeginBattle;
    Collider _collider;

    public event Action OnBossDeath = delegate { };

    // Use this for initialization
    protected override void Start()
    {
        base.Start();

        _collider = GetComponent<Collider>();

        _skeletonPositions = new Vector3[_skeletons.Length];
        for (int i = 0; i < _skeletons.Length; i++)
        {
            _skeletonPositions[i] = _skeletons[i].transform.position;
            _skeletons[i].gameObject.SetActive(false);
        }

        _targetShots = UnityEngine.Random.Range(_minAmountOfShots, _maxAmountOfShots);

        #region FSM
        var waiting = new State<BossActions>("Waiting");
        var idle = new State<BossActions>("Idle");
        var moving = new State<BossActions>("Moving");
        var shooting = new State<BossActions>("Shooting");
        var death = new State<BossActions>("Death");

        //Waiting
        waiting.AddTransition(BossActions.StartBattle, moving);

        //Idle
        idle.OnEnter += () =>
        {
            _collider.enabled = true;
            _anim.Play("Idle");
            SpawnBarriers();
            SpawnSkeletons();
        };
        idle.OnExit += () =>
        {
            _collider.enabled = false;
            DisableBarriers(); 
        };
        idle.AddTransition(BossActions.Damaged, moving);
        idle.AddTransition(BossActions.Death, death);

        //Moving
        moving.OnEnter += () =>
        {
            _collider.enabled = false;
            _navMesh.isStopped = false;
            _navMesh.ResetPath();
            _anim.SetFloat("Speed", 1);
            var currentWaypoint = _waypoints.OrderBy(x => Vector3.Distance(transform.position, x.position)).First();
            _currentWaypoint = GetNextWaypoint(currentWaypoint).position;
            _navMesh.SetDestination(_currentWaypoint);
        };
        moving.OnExit += () =>
        {
            _anim.SetFloat("Speed", 0);
            _navMesh.isStopped = true;
        };
        moving.AddTransition(BossActions.Moved, shooting);

        //Shooting
        shooting.OnEnter += () =>
        {
            _anim.Play("Shoot");
        };
        shooting.OnUpdate += () =>
        {
            transform.LookAt(_player.transform);
        };
        shooting.AddTransition(BossActions.Shooted, moving);
        shooting.AddTransition(BossActions.DoneShooting, idle);

        //Death
        death.OnEnter += () =>
        {
            _anim.Play("Die");
            OnBossDeath();
        };

        _fsm = new EventFSM<BossActions>(waiting);
        #endregion
    }

    // Update is called once per frame
    void Update()
    {
        _fsm.Update();

        if (Vector3.Distance(transform.position, _player.transform.position) < _rangeToBeginBattle)
        {
            _fsm.Feed(BossActions.StartBattle);
            _rangeToBeginBattle = 0;
        }

        if(Vector3.Distance(transform.position, _currentWaypoint) < 1)
        {
            _fsm.Feed(BossActions.Moved);
        }
    }

    Transform GetNextWaypoint(Transform current)
    {
        return _waypoints.Where(x => x != current)
                         .Skip(UnityEngine.Random.Range(0, _waypoints.Length - 2))
                         .First();
    }

    void Shoot()
    {
        transform.LookAt(_player.transform);

        var energyBall = Instantiate(_energyBallPrefab);
        energyBall.transform.position = _shootPoint.position;
        energyBall.transform.forward = transform.forward;

        _currentShots++;

        if (_currentShots >= _targetShots)
        {
            _fsm.Feed(BossActions.DoneShooting);
            _currentShots = 0;
            _targetShots = UnityEngine.Random.Range(_minAmountOfShots, _maxAmountOfShots);
        }
        else
            _fsm.Feed(BossActions.Shooted);
    }

    void SpawnBarriers()
    {
        foreach (var barrier in _barriers)
        {
            barrier.gameObject.SetActive(true);
        }
    }

    void DisableBarriers()
    {
        foreach (var barrier in _barriers)
        {
            barrier.Disable();
        }
    }

    void SpawnSkeletons()
    {
        for (int i = 0; i < _skeletons.Length; i++)
        {
            _skeletons[i].gameObject.SetActive(true);
            _skeletons[i].transform.position = _skeletonPositions[i];
            _skeletons[i].Health = _skeletons[i].MaxHealth;
            _skeletons[i].transform.LookAt(_player.transform);
        }
    }

    public override void Damage(int amount)
    {
        if (amount > 1) amount = 1;
        base.Damage(amount);

        if (_currentHealth > 0)
        {
            _fsm.Feed(BossActions.Damaged);
            _anim.Play("GetHit");
        }
        else
            _fsm.Feed(BossActions.Death);
    }
}
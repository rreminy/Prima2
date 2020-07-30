﻿using Newtonsoft.Json;
using Prima.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prima
{
    public class FFXIV3RoleQueue
    {
        [JsonProperty] private readonly List<(ulong, DateTime)> _dpsQueue;
        [JsonProperty] private readonly List<(ulong, DateTime)> _healerQueue;
        [JsonProperty] private readonly List<(ulong, DateTime)> _tankQueue;

        public FFXIV3RoleQueue()
        {
            _dpsQueue = new List<(ulong, DateTime)>();
            _healerQueue = new List<(ulong, DateTime)>();
            _tankQueue = new List<(ulong, DateTime)>();
        }

        public bool Enqueue(ulong userId, FFXIVRole role)
        {
            switch (role)
            {
                case FFXIVRole.DPS:
                    if (_dpsQueue.Any(tuple => tuple.Item1 == userId)) return false;
                    _dpsQueue.Add((userId, DateTime.UtcNow));
                    return true;
                case FFXIVRole.Healer:
                    if (_healerQueue.Any(tuple => tuple.Item1 == userId)) return false;
                    _healerQueue.Add((userId, DateTime.UtcNow));
                    return true;
                case FFXIVRole.Tank:
                    if (_tankQueue.Any(tuple => tuple.Item1 == userId)) return false;
                    _tankQueue.Add((userId, DateTime.UtcNow));
                    return true;
                default:
                    throw new NotImplementedException();
            }
        }

        public ulong? Dequeue(FFXIVRole role)
        {
            switch (role)
            {
                case FFXIVRole.DPS:
                    if (_dpsQueue.Count == 0) return null;
                    var (userd, _) = _dpsQueue[0];
                    _dpsQueue.RemoveAt(0);
                    return userd;
                case FFXIVRole.Healer:
                    if (_healerQueue.Count == 0) return null;
                    var (userh, _) = _healerQueue[0];
                    _healerQueue.RemoveAt(0);
                    return userh;
                case FFXIVRole.Tank:
                    if (_tankQueue.Count == 0) return null;
                    var (usert, _) = _tankQueue[0];
                    _tankQueue.RemoveAt(0);
                    return usert;
                default:
                    throw new NotImplementedException();
            }
        }

        public bool Remove(ulong userId, FFXIVRole role)
        {
            return role switch
            {
                FFXIVRole.DPS => _dpsQueue.Remove(tuple => tuple.Item1 == userId),
                FFXIVRole.Healer => _healerQueue.Remove(tuple => tuple.Item1 == userId),
                FFXIVRole.Tank => _tankQueue.Remove(tuple => tuple.Item1 == userId),
                _ => throw new NotImplementedException(),
            };
        }

        public int Count(FFXIVRole role)
        {
            return role switch
            {
                FFXIVRole.DPS => _dpsQueue.Count,
                FFXIVRole.Healer => _healerQueue.Count,
                FFXIVRole.Tank => _tankQueue.Count,
                _ => throw new NotImplementedException(),
            };
        }

        public int CountDistinct()
        {
            return _dpsQueue
                .Concat(_healerQueue)
                .Concat(_tankQueue)
                .Select(tuple => tuple.Item1)
                .Distinct()
                .Count();
        }

        public int GetPosition(ulong userId, FFXIVRole role)
        {
            return role switch
            {
                FFXIVRole.DPS => _dpsQueue.IndexOf(tuple => tuple.Item1 == userId) + 1,
                FFXIVRole.Healer => _healerQueue.IndexOf(tuple => tuple.Item1 == userId) + 1,
                FFXIVRole.Tank => _tankQueue.IndexOf(tuple => tuple.Item1 == userId) + 1,
                _ => throw new NotImplementedException(),
            };
        }

        public (IEnumerable<ulong>, IEnumerable<ulong>, IEnumerable<ulong>) Timeout(double secondsBeforeNow)
        {
            var dpsTimedOut = _dpsQueue.RemoveAll(tuple => (DateTime.UtcNow - tuple.Item2).TotalSeconds > secondsBeforeNow, overload: true)
                .Select(tuple => tuple.Item1);
            var healersTimedOut = _healerQueue.RemoveAll(tuple => (DateTime.UtcNow - tuple.Item2).TotalSeconds > secondsBeforeNow, overload: true)
                .Select(tuple => tuple.Item1);
            var tanksTimedOut = _tankQueue.RemoveAll(tuple => (DateTime.UtcNow - tuple.Item2).TotalSeconds > secondsBeforeNow, overload: true)
                .Select(tuple => tuple.Item1);

            return (dpsTimedOut, healersTimedOut, tanksTimedOut);
        }
    }

    [Flags]
    public enum FFXIVRole
    {
        None = 0,
        DPS = 1,
        Healer = 2,
        Tank = 4,
    }
}
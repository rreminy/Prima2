﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Prima.Models
{
    public class GlobalConfiguration
    {
        [BsonId]
        [BsonRequired]
        private readonly ObjectId _id;

        [BsonRequired]
        [BsonRepresentation(BsonType.String)]
        public ulong BotMaster;

        [BsonRequired]
        [BsonRepresentation(BsonType.String)]
        public char Prefix;

        [BsonRequired]
        public string TempDir;
    }
}
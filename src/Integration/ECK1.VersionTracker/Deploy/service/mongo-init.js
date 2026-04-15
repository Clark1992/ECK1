// MongoDB initialization script for VersionTracker
// Creates the entity_versions collection with required indexes.
// Run once against the target database:
//   mongosh <connectionString>/eck1_versions mongo-init.js

db = db.getSiblingDB("eck1_versions");

db.createCollection("entity_versions");

// Unique index on Key (composite "EntityType:EntityId") — primary lookup
db.entity_versions.createIndex(
  { Key: 1 },
  { unique: true, name: "idx_key_unique" }
);

print("VersionTracker MongoDB initialization complete.");

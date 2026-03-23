const config = {
  mongodb: {
    url: process.env.MONGO_URL,
    databaseName: process.env.MONGO_DB,
    options: {}
  },
  migrationsDir: "migrations",
  changelogCollectionName: "changelog",
  lockCollectionName: "changelog_lock",
  lockTtl: 0,
  migrationFileExtension: ".js",
  useFileHash: false,
  moduleSystem: 'commonjs',
};

module.exports = config;

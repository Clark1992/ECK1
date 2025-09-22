const collectionName = 'samples';

module.exports = {
  /**
   * @param db {import('mongodb').Db}
   * @param client {import('mongodb').MongoClient}
   * @returns {Promise<void>}
   */
  async up(db, _) {
    const collections = await db.listCollections({ name: collectionName }).toArray();
    if (collections.length === 0) {
      await db.createCollection(collectionName);
    }

    await db.collection(collectionName).createIndex(
      { sampleId: 1 },
      { unique: true, name: "idx_sampleId" }
    );
  },

  /**
   * @param db {import('mongodb').Db}
   * @param client {import('mongodb').MongoClient}
   * @returns {Promise<void>}
   */
  async down(db, _) {
    try {
      await db.collection(collectionName).dropIndex("idx_sampleId");
    } catch (e) {
      console.warn(`❗ Index not found: ${e.message}`);
    }

    try {
      await db.collection(collectionName).drop();
    } catch (e) {
      console.warn(`❗ Collection not found: ${e.message}`);
    }
  }
};

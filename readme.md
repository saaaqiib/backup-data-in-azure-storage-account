This will copy all new/changed blobs and containers in source storage and copy/overwrite them into destination storage so destination storage has everything that the source storage has with the exact version.

This works in one direction so anything on destination storage is not copied back to source meaning you only need blob data reader permission on the source storage and blob data contributor permission on the destination storage on the user assigned managed identity of the function app

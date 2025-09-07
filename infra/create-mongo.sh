# notes: use the local network, use /data/db to persist the data between container restarts
docker network create local

docker run -d --name balance-mongo -p 27017:27017 -v balance-mongo:/data/db --network local mongodb/mongodb-community-server

## run this after you pay the bill
mongosh --quiet <<EOF
use balance
db.expenses.deleteMany({})
quit()
EOF
import { Link } from "react-router-dom";

function DishCard({ dish }) {
  return (
    <div className="dish-card">
      <h3>
        <Link to={`/dish/${dish.id}`}>
          {dish.name}
        </Link>
      </h3>
      <p>Rs. {Number(dish.price).toFixed(2)}</p>
      {dish.outlet && <p>{dish.outlet}</p>}
      <small>{dish.category}</small>
      {!dish.available && <span> — Sold out</span>}
    </div>
  );
}
export default DishCard;

import { useState } from "react";
function OrderPage() {
  const [form, setForm] = useState({ name: "", email: "", qty: 1 });
  const [errors, setErrors] = useState({});
  const [done, setDone] = useState(false);
  function validate(v) {
    const e = {};
    if (v.name.trim().length < 2) e.name = "Name too short";
    if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(v.email))
      e.email = "Enter a valid email";
    if (Number(v.qty) < 1) e.qty = "Qty must be ≥ 1";
    return e;
  }
  function handleChange(e) {
    setForm({ ...form, [e.target.name]: e.target.value });
  }
  function handleSubmit(e) {
    e.preventDefault();
    const found = validate(form);
    setErrors(found);
    if (Object.keys(found).length === 0) setDone(true);
  }
  if (done) return <p>Thanks, {form.name}! Order received.</p>;
  return (
    <form onSubmit={handleSubmit}>
      <input name="name" value={form.name} onChange={handleChange} />
      {errors.name && <span> {errors.name}</span>}
      <input name="email" value={form.email} onChange={handleChange} />
      {errors.email && <span> {errors.email}</span>}
      <input
        name="qty"
        type="number"
        value={form.qty}
        onChange={handleChange}
      />
      {errors.qty && <span> {errors.qty}</span>}
      <button type="submit">Place order</button>
    </form>
  );
}
export default OrderPage;

import { Link } from "react-router-dom";

function App() {
  return (
    <div style={{ padding: "1rem" }}>
      <h1>InvoiceBilling</h1>

      <nav>
        <ul>
          <li>
            <Link to="/customers">Customers</Link>
          </li>
          <li>
            <Link to="/products">Products</Link>
          </li>
          {/* Later: add links for Products, Invoices, Dashboard, etc. */}
        </ul>
      </nav>

      <p>Welcome to InvoiceBilling. Use the navigation above to manage data.</p>
    </div>
  );
}

export default App;

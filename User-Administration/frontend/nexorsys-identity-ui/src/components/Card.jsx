import React from "react";

const Card = ({ children, className = "" }) => {
  const baseClasses = "bg-nexorsys-gray rounded-lg p-6 shadow-lg ";

  return <div className={`${baseClasses}${className}`}>{children}</div>;
};

export default Card;
